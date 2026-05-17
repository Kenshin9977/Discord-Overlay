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

### 2. Bootstrap login (one time only)

This is the **only** manual login, ever — to bring the session up the
first time. From then on the keepalive in section 4 refreshes it
autonomously; you never connect to the VPS for a release.

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

Create `/opt/sign/sign-stdin.sh` (owned by the signing user, `chmod 755`).
It is **self-healing**: if signing fails because the SimplySign session
lapsed, it triggers a re-login (section 4) and retries once, so a stale
session never reaches CI.

```bash
#!/usr/bin/env bash
set -euo pipefail
export P11_KIT_SERVER_ADDRESS="unix:path=/run/user/1000/p11-kit/p11kit.sock"
in=$(mktemp); out=$(mktemp)
trap 'rm -f "$in" "$out"' EXIT
cat > "$in"

do_sign() {
  osslsigncode sign \
    -pkcs11module /usr/lib/x86_64-linux-gnu/p11-kit-client.so \
    -pkcs11cert 'pkcs11:model=SimplySign%20C' \
    -key        'pkcs11:model=SimplySign%20C' \
    -h sha256 -t http://time.certum.pl/ \
    -n "Discord-Overlay" \
    -i "https://github.com/Kenshin9977/Discord-Overlay" \
    -in "$in" -out "$out" >&2
}

if ! do_sign; then
  echo "sign-stdin: signing failed, forcing a SimplySign re-login…" >&2
  sudo -n /usr/local/bin/certum-relogin   # scoped sudoers, see section 4
  do_sign                                 # retry once; hard-fail if it
fi                                        # still fails (fail-closed)
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

### 4. Autonomous session keepalive (no manual step, ever)

SimplySign cloud sessions expire (you pick the duration at login;
Certum caps it). To keep releases fully unattended, the VPS re-logs in
by itself: a timer refreshes the session *before* it lapses, and
`sign-stdin.sh` also forces a re-login on demand if a sign ever fails.
You never touch the VPS for a release.

**One-time: store the TOTP secret on the VPS.** Microsoft Authenticator
won't reveal it after enrollment — re-enroll the SimplySign 2FA and
save the base32 `secret=` from the `otpauth://totp/...?secret=...`
string. It stays on the VPS only, never in CI:

```bash
sudo install -m 700 -d /opt/sign/secret
printf '%s' 'YOURBASE32SECRET' | sudo tee /opt/sign/secret/totp >/dev/null
sudo chmod 600 /opt/sign/secret/totp
echo -n 'YOUR_SIMPLYSIGN_USERID' | sudo tee /opt/sign/secret/userid >/dev/null
```

**The re-login script** `/usr/local/bin/certum-relogin` (`chmod 755`,
root-owned) generates the current code with `oathtool` and drives the
SimplySign Desktop window inside the container with `xdotool`, then
verifies the token is back before returning success:

```bash
#!/usr/bin/env bash
set -euo pipefail
export DISPLAY=:0   # the container's X server
secret=$(cat /opt/sign/secret/totp)
userid=$(cat /opt/sign/secret/userid)
otp=$(oathtool --totp -b "$secret")

# --- Brittle bit, isolated here: the SimplySign Desktop window layout.
win=$(xdotool search --name 'SimplySign' | head -n1)
xdotool windowactivate --sync "$win"
xdotool type --delay 60 "$userid"; xdotool key Tab
xdotool type --delay 60 "$otp";    xdotool key Return
sleep 8
# Some builds show a confirmation dialog that must be closed:
xdotool search --name 'SimplySign' | while read -r w; do
  xdotool windowactivate --sync "$w" key Return || true
done

# Verify: token must be listable, else fail loudly (no false "ok").
for _ in $(seq 1 20); do
  if P11_KIT_SERVER_ADDRESS=unix:path=/run/user/1000/p11-kit/p11kit.sock \
       p11tool --list-tokens 2>/dev/null | grep -qi simplysign; then
    echo "certum-relogin: session OK"; exit 0
  fi
  sleep 3
done
echo "certum-relogin: token still absent after re-login" >&2
exit 1
```

> If running `xdotool`/`p11tool` against the container needs to happen
> *inside* it, wrap the body in `docker exec certum-signer …`. Adjust
> the window name / key sequence here if Certum changes the UI — this
> is the one fragile spot, kept off the build path.

**Timer** — refresh ahead of expiry (set the interval below your chosen
session lifetime, e.g. 45 min for a 1 h session):

```ini
# /etc/systemd/system/certum-relogin.service
[Service]
Type=oneshot
ExecStart=/usr/local/bin/certum-relogin

# /etc/systemd/system/certum-relogin.timer
[Timer]
OnBootSec=2min
OnUnitActiveSec=45min
Persistent=true
[Install]
WantedBy=timers.target
```

```bash
sudo systemctl enable --now certum-relogin.timer
```

**Scoped sudoers** so the restricted signing user can trigger an
on-demand re-login (and nothing else):

```
sign ALL=(root) NOPASSWD: /usr/local/bin/certum-relogin
```

Net result: the timer keeps the session warm; if it ever still lapses
between ticks, the first sign of a release self-heals and retries; if
even that fails, the release fails closed (never unsigned). No manual
VPS step in the normal path.

## Failure modes

- **No secrets:** unsigned release, CI green. By design.
- **Session expired:** the keepalive timer normally prevents it; if it
  still lapses, `sign-stdin.sh` self-heals (forces a re-login, retries
  once) — release stays automated.
- **Re-login itself broken** (Certum changed the UI, TOTP secret stale):
  retry fails, `sign-remote.sh` sees non‑PE/too‑small output and fails
  the job. Fail-closed: never a silently-unsigned release. Fix
  `certum-relogin`, re-run.
- **VPS unreachable:** SSH step fails the job; re-run after fixing.
- **Cert reissued:** update the forced-command script if the
  `pkcs11:model` changes; update the thumbprint in this doc.
