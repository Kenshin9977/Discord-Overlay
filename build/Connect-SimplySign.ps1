<#
.SYNOPSIS
Logs in to Certum SimplySign Desktop non-interactively so a cloud
code-signing certificate becomes available to signtool.exe in CI.

.DESCRIPTION
Certum's "Open Source" code-signing certificates keep their private key
in the SimplySign cloud, gated by a TOTP second factor. SimplySign
Desktop mounts that cloud key as a virtual smart card; once mounted,
the certificate shows up in the CurrentUser\My store and signtool can
use it via its thumbprint.

There is no headless login API, so we drive the SimplySign Desktop
window with SendKeys after generating the current TOTP code ourselves
from the otpauth:// secret. This is inherently brittle (it depends on
SimplySign Desktop's window layout); the SendKeys sequence in step 4 is
the one place you may need to tweak if Certum changes the UI.

.NOTES
Required environment variables (wired from GitHub secrets in CI):
  CERTUM_OTP_URI   The full otpauth://totp/...?secret=... URI captured
                   when enrolling the SimplySign 2FA. Microsoft
                   Authenticator will NOT reveal this after the fact —
                   you must re-enroll the authenticator in the
                   SimplySign account and save the secret/URI then.
  CERTUM_USERID    SimplySign user ID (the cloud signing account login).
  CERTUM_EXE_PATH  Full path to SimplySignDesktop.exe.
  CERTUM_CERT_THUMBPRINT
                   Expected signing-cert thumbprint; the script waits
                   until a cert with this thumbprint appears in
                   Cert:\CurrentUser\My, then exits 0. Fails (exit 1)
                   if it never appears.
#>

$ErrorActionPreference = 'Stop'

$OtpUri     = $env:CERTUM_OTP_URI
$UserId     = $env:CERTUM_USERID
$ExePath    = $env:CERTUM_EXE_PATH
$Thumbprint = ($env:CERTUM_CERT_THUMBPRINT -replace '\s', '').ToUpperInvariant()

foreach ($pair in @{ CERTUM_OTP_URI = $OtpUri; CERTUM_USERID = $UserId; CERTUM_EXE_PATH = $ExePath; CERTUM_CERT_THUMBPRINT = $Thumbprint }.GetEnumerator()) {
    if ([string]::IsNullOrWhiteSpace($pair.Value)) {
        throw "Missing required environment variable: $($pair.Key)"
    }
}
if (-not (Test-Path $ExePath)) {
    throw "SimplySign Desktop not found at CERTUM_EXE_PATH: $ExePath"
}

# === Parse the otpauth:// URI =================================================
$uri = [Uri]$OtpUri
$q = @{}
foreach ($part in $uri.Query.TrimStart('?') -split '&') {
    $kv = $part -split '=', 2
    if ($kv.Count -eq 2) { $q[$kv[0]] = [Uri]::UnescapeDataString($kv[1]) }
}

$Base32    = $q['secret']
$Digits    = ($q['digits'] -as [int]); if (-not $Digits) { $Digits = 6 }
$Period    = ($q['period'] -as [int]); if (-not $Period) { $Period = 30 }
$Algorithm = (($q['algorithm']) ?? 'SHA1').ToUpper()
if ([string]::IsNullOrWhiteSpace($Base32)) {
    throw "Could not find 'secret' in CERTUM_OTP_URI."
}
if ($Algorithm -ne 'SHA1') {
    throw "This helper only implements HMAC-SHA1 (requested: $Algorithm)."
}

# === TOTP generator ==========================================================
Add-Type -Language CSharp @"
using System;
using System.Security.Cryptography;

public static class Totp
{
    private const string B32 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private static byte[] Base32Decode(string s)
    {
        s = s.TrimEnd('=').ToUpperInvariant();
        int byteCount = s.Length * 5 / 8;
        byte[] bytes = new byte[byteCount];
        int bitBuffer = 0, bitsLeft = 0, idx = 0;
        foreach (char c in s)
        {
            int val = B32.IndexOf(c);
            if (val < 0) throw new ArgumentException("Invalid Base32 char: " + c);
            bitBuffer = (bitBuffer << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bytes[idx++] = (byte)(bitBuffer >> (bitsLeft - 8));
                bitsLeft -= 8;
            }
        }
        return bytes;
    }

    public static string Now(string secret, int digits, int period)
    {
        byte[] key = Base32Decode(secret);
        long counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / period;
        byte[] cnt = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(cnt);
        byte[] hash = new HMACSHA1(key).ComputeHash(cnt);
        int offset = hash[hash.Length - 1] & 0x0F;
        int binary =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);
        int otp = binary % (int)Math.Pow(10, digits);
        return otp.ToString(new string('0', digits));
    }
}
"@

$otp = [Totp]::Now($Base32, $Digits, $Period)
Write-Host "Generated a $Digits-digit TOTP (valid for the current $Period s window)."

# === Launch SimplySign Desktop and send credentials ==========================
$proc = Start-Process -FilePath $ExePath -PassThru
Write-Host "Started SimplySign Desktop (pid $($proc.Id)); waiting for the login window…"
Start-Sleep -Seconds 8

$wshell = New-Object -ComObject WScript.Shell
$focused = $false
for ($i = 0; -not $focused -and $i -lt 15; $i++) {
    $focused = $wshell.AppActivate($proc.Id) -or $wshell.AppActivate('SimplySign Desktop')
    if (-not $focused) { Start-Sleep -Milliseconds 700 }
}
if (-not $focused) {
    throw "Could not bring the SimplySign Desktop window to the foreground."
}

Start-Sleep -Milliseconds 600
# --- Brittle bit: window field order. Default assumes the login dialog
# --- focuses the User ID field first, then Tab to the OTP field, Enter
# --- to submit. If Certum changes the layout, adjust here.
$wshell.SendKeys($UserId)
Start-Sleep -Milliseconds 300
$wshell.SendKeys('{TAB}')
Start-Sleep -Milliseconds 300
$wshell.SendKeys($otp)
Start-Sleep -Milliseconds 300
$wshell.SendKeys('{ENTER}')
Write-Host "Submitted SimplySign credentials; waiting for the virtual smart card to mount…"

# === Wait for the certificate to appear ======================================
$deadline = (Get-Date).AddSeconds(90)
while ((Get-Date) -lt $deadline) {
    $match = Get-ChildItem Cert:\CurrentUser\My -ErrorAction SilentlyContinue |
        Where-Object { $_.Thumbprint -eq $Thumbprint }
    if ($match) {
        Write-Host "Signing certificate $Thumbprint is available (subject: $($match.Subject))."
        exit 0
    }
    Start-Sleep -Seconds 3
}

throw "Timed out after 90 s: certificate $Thumbprint never appeared in Cert:\CurrentUser\My. " +
      "SimplySign login likely failed (wrong OTP/User ID, or the SendKeys sequence no longer matches the window)."
