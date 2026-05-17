# Code signing

Release binaries are signed with a **Certum "Open Source Developer"
code-signing certificate** whose private key lives in Certum's
**SimplySign cloud** (it is not exportable — there is no PFX). Signing
runs entirely in the GitHub-hosted release workflow.

Signing is **opt-in and non-breaking**: if the secrets below are not
configured, `release.yml` still builds and publishes **unsigned**
artifacts. Configure the secrets when you are ready and the next tagged
release is signed automatically — no code change required.

## How it works

1. `release.yml` detects whether `CERTUM_OTP_URI` is set.
2. If set: it installs SimplySign Desktop (winget), then
   `build/Connect-SimplySign.ps1` generates the current TOTP from the
   enrolled secret and drives the SimplySign Desktop login. SimplySign
   mounts the cloud key as a virtual smart card, so the certificate
   appears in `Cert:\CurrentUser\My`.
3. `vpk pack --signParams` calls `signtool.exe` to sign the app exe,
   `Update.exe`, and `Setup.exe`, selecting the cert by thumbprint
   (`/sha1`) and timestamping via `http://time.certum.pl`.

## Required GitHub Actions secrets

| Secret | Value |
| --- | --- |
| `CERTUM_OTP_URI` | The full `otpauth://totp/...?secret=...` URI of the SimplySign 2FA. **See the prerequisite below.** |
| `CERTUM_USERID` | Your SimplySign / Certum cloud signing account user ID. |
| `CERTUM_CERT_THUMBPRINT` | *(optional)* Signing-cert SHA‑1 thumbprint. Defaults to `80C0A61E3A5E10199070235AE95A9A0DB6971A94` (the current cert). Set this only if the cert is reissued. |

## Prerequisite: capturing the TOTP secret

The automation needs the raw TOTP **secret** (the `otpauth://` URI), not
just 6-digit codes. **Microsoft Authenticator does not let you reveal a
secret after enrollment.** To obtain it:

1. Sign in to your Certum / SimplySign account settings.
2. Re-enroll the two-factor authenticator. During enrollment Certum
   shows a QR code **and** a manual setup key / `otpauth://` URI.
3. Save that `otpauth://totp/...?secret=...` string — that is the value
   for the `CERTUM_OTP_URI` secret. (You can still scan the same QR with
   Microsoft Authenticator for your own interactive use.)
4. Store it as a GitHub Actions secret. Treat it like a password: it is
   the second factor for your signing key.

## Known fragility

`Connect-SimplySign.ps1` drives the SimplySign Desktop window with
`SendKeys` because Certum offers no headless login. The default key
sequence assumes the login dialog focuses **User ID**, then `Tab` to the
**OTP** field, then `Enter`. If Certum changes the window layout, adjust
the SendKeys block in step 4 of that script. The script waits up to 90 s
for the certificate to appear and fails the build with a clear message
if login did not take, so a broken sequence fails fast rather than
producing unsigned-but-green releases.
