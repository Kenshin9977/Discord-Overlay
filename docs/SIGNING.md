# Code signing

Release binaries are signed with a **Certum "Open Source Developer"
code-signing certificate** (SHA‑1 thumbprint
`80C0A61E3A5E10199070235AE95A9A0DB6971A94`, valid 2026‑05‑17 →
**2027‑05‑17 — renew before then**). The private key is non‑exportable and
lives in the **Certum SimplySign cloud**: there is no PFX.

## Architecture

Signing runs on GitHub‑hosted CI, but neither the key nor the credential
that reaches it ever touches CI.

A Linux VPS (`92.222.247.17`, OVH, Ubuntu 24.04) is the **signing host**:

- `ssign` signs against **Certum's cloud API over plain HTTPS**. No
  SimplySign Desktop, no container, no X server, no PKCS#11.
- A locked‑down SSH **forced command** is the only thing the CI key can
  reach: pipe an unsigned binary in on stdin, read the signed binary back
  on stdout. No shell, no arguments, no paths.
- `vpk pack --signTemplate` makes CI call `build/sign-remote.sh`, which
  streams each binary (app exe, `Update.exe`, `Setup.exe`) to the VPS.

The TOTP seed is the **master credential** — whoever holds it can sign as
you until you re-enrol. It stays on the VPS. Putting it in a GitHub secret
would be simpler and much worse: a CI compromise would then grant indefinite
signing capability. The SSH indirection is what prevents that, and it is the
one part of this design that is load-bearing for security.

Signing is **opt‑in and non‑breaking**: with no signing secrets,
`release.yml` builds **unsigned** binaries exactly as before.

## Files

| Path | Role |
| --- | --- |
| `build/sign-remote.sh` | Runs in CI. SSHes one binary to the VPS, gets it back signed. |
| `build/vps/sign-stdin.sh` | Runs on the VPS as the SSH forced command. |
| `build/vps/install-signer.sh` | Builds and installs `ssign` + the signer on the VPS. Idempotent. Pins the upstream commit. |

## GitHub Actions secrets

| Secret | Value |
| --- | --- |
| `SIGN_SSH_HOST` | `sign@92.222.247.17` |
| `SIGN_SSH_KEY` | The CI private key (printed once during provisioning; not stored on the VPS). |
| `SIGN_SSH_KNOWN_HOSTS` | `92.222.247.17 ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIEFkW6/XG9IdV4sPQXmEhRD1f+QuCELpDynee5DkXLrY` |
| `SIGN_SSH_PORT` | *(optional, default 22)* |

## Deploying / redeploying the signer

On the VPS:

```bash
curl -fsSL https://raw.githubusercontent.com/Kenshin9977/Discord-Overlay/master/build/vps/install-signer.sh | bash
```

Then check it end to end — this is the exact path CI takes:

```bash
sudo -u sign /opt/sign/sign-stdin.sh < some.exe > signed.exe
osslsigncode verify signed.exe | grep -E 'Timestamp verified|Succeeded'
```

`ssign` is pinned to an upstream commit on purpose: it is a young,
single-author project sitting on the path to the signing key. A pinned SHA
cannot be swapped out by a force-push — the build breaks instead of silently
changing. Re-read the diff before moving the pin.

Currently pinned to **v0.1.5** (`dbe6751`). Both Authenticode bugs below were
carried here as local patches against the previous pin and are fixed upstream
as of that release, so `install-signer.sh` no longer patches anything — it
asserts both fixes are present in the checkout and refuses to build if either
is missing.

## The three traps (all cost a lot to find — do not re-introduce them)

### 1. The timestamp OID

`ssign` before v0.1.2 embedded the RFC3161 token under the **generic CMS**
OID `1.2.840.113549.1.9.16.2.14`. Authenticode does not read that attribute —
Windows requires `1.3.6.1.4.1.311.3.3.1`.

It fails **silently**: `ssign` reports success, the TSA really is contacted,
the token really is in the file, and Windows reports *no timestamp at all*.
The signature is valid today and goes **invalid the moment the certificate
expires (2027‑05‑17)** — including on binaries already shipped to users.

Fixed upstream in v0.1.2 and covered by an upstream test. `install-signer.sh`
asserts the correct OID is in the checkout before it builds, so a pin move
that lost it fails the install rather than the next certificate renewal.

**Always verify on Windows before shipping:**

```powershell
$s = Get-AuthenticodeSignature .\signed.exe
$s.Status                   # must be Valid
$s.TimeStamperCertificate   # must NOT be null
```

### 2. The 8-byte alignment padding

`prepare()` hashed the file as it stood, but `embed()` then zero-padded it to
an 8-byte boundary before appending the certificate table — and that padding
lands **inside** the region Windows hashes. Every PE whose length was not
already a multiple of 8 therefore got a signature Windows reports as
**HashMismatch**: "the file has been changed by an unauthorized user or
process". That is worse than shipping unsigned, because the binary looks
tampered with.

It hides perfectly behind an 8-aligned test file. The fixture used here was
125440 bytes (8 × 15680) and verified clean while Velopack's real `Setup.exe`
did not, and a broken signature reached a published release before it was
caught.

Fixed upstream in v0.1.2, with a regression test that signs a deliberately
misaligned fixture. `install-signer.sh` asserts the padding is hashed before
it builds.

### 3. One login per TOTP window

Certum rate‑limits logins per TOTP code: **two in the same 30‑second window
succeed, a third is refused.** A release signs 3–4 files, each its own
invocation, so a naive signer fails at random. `sign-stdin.sh` serialises on
a lock and spends at most one login per window.

Since v0.1.5 `ssign` also caches the cloud session (`$HOME/.cache/ssign/session.json`,
0600, valid ~30 min), and every file in a release is signed by the same `sign`
user, so the second and later files normally reuse that session and skip both
the login and the wait. The lock and the one-login-per-window rule still apply
whenever a fresh login is actually needed.

## TOTP

`/opt/sign/secret/totp` holds the **full `otpauth://` URI**, not a bare
secret, because the URI carries `algorithm`/`digits`/`period` and nothing
then has to guess them. For this account: **SHA‑256, 6 digits, 30 s**.

> **Microsoft Authenticator cannot serve as the source of truth.** It
> silently ignores `algorithm=SHA256` and always computes SHA‑1, so the codes
> it shows are rejected by Certum. If you re-enrol, capture the `otpauth://`
> URI itself — not a QR screenshot, not the digits.

Re-enrolling 2FA **invalidates the previous seed**. If logins start failing,
suspect the seed before suspecting anything else: an entire earlier attempt at
this pipeline was spent tuning the TOTP algorithm while the stored seed simply
did not match the enrolled one.

## Failure modes

- **No secrets:** unsigned release, CI green. By design.
- **Signing fails:** `sign-stdin.sh` exits non-zero rather than return the
  input untouched, and `sign-remote.sh` additionally checks the returned bytes
  are a PE and larger than the input. The release fails red; it never ships a
  binary that is silently unsigned.
- **`no authorization code after login`:** wrong e-mail, wrong or stale seed,
  or a reused TOTP window. Check the seed first.
- **VPS unreachable:** the SSH step fails the job; re-run after fixing.
