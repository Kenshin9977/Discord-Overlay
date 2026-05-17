# Code signing

Release binaries are signed with a **Certum "Open Source Developer"
code-signing certificate** (SHA‑1 thumbprint
`80C0A61E3A5E10199070235AE95A9A0DB6971A94`, valid 2026‑05‑17 →
2027‑05‑17). The private key is non‑exportable and lives in **Certum
SimplySign cloud** — there is no PFX and no headless login API.

## Why this design

Certum only unlocks the cloud key after an interactive TOTP login. Doing
that inside CI means brittle GUI automation on every build. Instead, the
**Linux VPS is a persistent signing host**:

- SimplySign is logged in **once** on the VPS; the cloud session then
  persists and is usable from automation without the GUI.
- The VPS exposes signing through a **locked‑down SSH forced command**:
  pipe a binary in, get a signed binary out — nothing else.
- CI stays on GitHub‑hosted runners. For each binary, `vpk` calls
  `build/sign-remote.sh`, which streams the file to the VPS and back.
- The certificate, the TOTP secret, and the SimplySign session **never
  touch CI**. The CI key can only ask the VPS to sign a blob.

Signing is **opt‑in and non‑breaking**: with no signing secrets,
`release.yml` builds **unsigned** binaries exactly as before.

## GitHub Actions secrets

| Secret | Value |
| --- | --- |
| `SIGN_SSH_HOST` | `sign@your-vps` (the restricted signing account). |
| `SIGN_SSH_KEY` | Private key authorised for that account (the CI half of the keypair below). |
| `SIGN_SSH_PORT` | *(optional)* SSH port, default `22`. |
| `SIGN_SSH_KNOWN_HOSTS` | *(recommended)* `known_hosts` line pinning the VPS host key. Without it the host key is trusted on first use. Get it with `ssh-keyscan -p <port> your-vps`. |

## VPS setup (one time)

### 1. Run the SimplySign signing container

The key lives in SimplySign's cloud; on Linux it is reached through a
p11‑kit socket published by SimplySign Desktop running headless. The
maintained [`hpvb/certum-container`](https://github.com/hpvb/certum-container)
packages exactly this.

```bash
# on the VPS
git clone https://github.com/hpvb/certum-container
cd certum-container
docker build -t certum-signer .
mkdir -p /opt/sign/p11
docker run -d --name certum-signer --restart unless-stopped \
  -p 127.0.0.1:5999:5999 \
  -v /opt/sign/p11:/run/user/1000/p11-kit \
  certum-signer
```

### 2. Log in once (establishes the persistent session)

Tunnel the VNC port and connect a VNC client:

```bash
ssh -L 5999:127.0.0.1:5999 your-vps
# point a VNC client at localhost:5999
```

In the SimplySign Desktop window: enter your **User ID**, then the
**OTP** from the SimplySign mobile app (Microsoft Authenticator works if
you enrolled it). **Press the dialog's Close button after login** — the
token is inert until you do. The p11‑kit socket now appears under
`/opt/sign/p11/`.

### 3. Restricted signing account + forced command

Create `/opt/sign/sign-stdin.sh` (owned by the signing user, `chmod 755`):

```bash
#!/usr/bin/env bash
set -euo pipefail
export P11_KIT_SERVER_ADDRESS="unix:path=/run/user/1000/p11-kit/p11kit.sock"
in=$(mktemp); out=$(mktemp)
trap 'rm -f "$in" "$out"' EXIT
cat > "$in"
osslsigncode sign \
  -pkcs11module /usr/lib/x86_64-linux-gnu/p11-kit-client.so \
  -pkcs11cert 'pkcs11:model=SimplySign%20C' \
  -key        'pkcs11:model=SimplySign%20C' \
  -h sha256 -t http://time.certum.pl/ \
  -n "Discord-Overlay" \
  -i "https://github.com/Kenshin9977/Discord-Overlay" \
  -in "$in" -out "$out" >&2
cat "$out"
```

> The `pkcs11:model=...` string and the `p11-kit-client.so` path can
> differ per token/distro. List what's actually exposed with
> `P11_KIT_SERVER_ADDRESS=unix:path=/opt/sign/p11/p11kit.sock p11tool --list-tokens`
> and adjust.

Generate a CI keypair (no passphrase, it's used unattended):

```bash
ssh-keygen -t ed25519 -N '' -C 'ci@discord-overlay' -f ci_sign_key
```

Put the **private** key in the `SIGN_SSH_KEY` GitHub secret. On the VPS,
add the **public** key to the signing user's `~/.ssh/authorized_keys`
constrained to the forced command:

```
command="/opt/sign/sign-stdin.sh",restrict ssh-ed25519 AAAA...ci@discord-overlay
```

`restrict` removes pty/port/agent/X11 forwarding. This key can do
nothing but produce a signed copy of whatever is piped in.

Smoke test from your laptop:

```bash
ssh -i ci_sign_key sign@your-vps < some.exe > some-signed.exe
osslsigncode verify some-signed.exe   # or check the signature on Windows
```

### 4. Keeping the session alive (the only recurring chore)

SimplySign cloud sessions expire (you pick the duration at login; Certum
caps it). When the session lapses, signing fails and **CI goes red**
(`sign-remote.sh` rejects non‑PE output) rather than shipping unsigned
binaries. Options, least to most automated:

- **Manual:** re-do step 2 over VNC when a release fails.
- **Scripted re-login:** store the `otpauth://` TOTP secret in a
  root‑only file **on the VPS**, and a `systemd` timer that runs
  `oathtool --totp -b "$SECRET"` and types User ID + code into the
  container's SimplySign window via `xdotool` (the container already
  runs an X server). Schedule it just under the session lifetime. The
  brittleness is now isolated to the VPS, off the build path, and
  observable.

Whichever you pick, the TOTP secret stays on the VPS and never enters
CI. To capture that secret: Microsoft Authenticator won't reveal it
after enrollment — re-enroll the SimplySign 2FA and save the
`otpauth://totp/...?secret=...` string shown during setup.

## Failure modes

- **No secrets:** unsigned release, CI green. By design.
- **Session expired / login broken:** `sign-remote.sh` sees non‑PE or
  too‑small output and fails the job. No silently-unsigned release.
- **VPS unreachable:** SSH step fails the job; re-run after fixing.
- **Cert reissued:** update the forced-command script if the
  `pkcs11:model` changes; update the thumbprint in this doc.
