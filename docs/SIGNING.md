# Code signing

Release binaries are signed with a **Certum "Open Source Developer"
code-signing certificate** (SHA‑1 thumbprint
`80C0A61E3A5E10199070235AE95A9A0DB6971A94`, valid 2026‑05‑17 →
2027‑05‑17). The private key is non‑exportable and lives in **Certum
SimplySign cloud** — there is no PFX and no headless login API.

## Architecture

Signing runs on GitHub‑hosted CI but the key never touches CI. A Linux
VPS (`92.222.247.17`, OVH, Ubuntu 24.04) is a **persistent signing
host**:

- SimplySign Desktop runs there headless in a container, logged in once;
  the cloud session then persists and signs via `osslsigncode` +
  `p11-kit`.
- A locked‑down SSH **forced command** exposes signing only: pipe a
  binary in, get a signed binary out, nothing else.
- `vpk pack --signTemplate` makes CI call `build/sign-remote.sh`, which
  streams each binary (app exe, `Update.exe`, `Setup.exe`) to the VPS
  and back.
- The session is kept alive **autonomously**: a `systemd` timer
  re-logs in before expiry, and the signer self-heals (forces a
  re-login + retries once) if a sign hits a stale session. Fails closed
  if re-login itself breaks — never a silently-unsigned release.

Signing is **opt‑in and non‑breaking**: with no signing secrets,
`release.yml` builds **unsigned** binaries exactly as before.

## What is already deployed on the VPS

Provisioned on `92.222.247.17` (do not redo):

- Toolchain: `osslsigncode`, `p11-kit`, `p11-kit-modules`, `oathtool`,
  `xdotool`, `gnutls-bin`.
- Restricted user `sign` (uid 1002): no password, no sudo (except the
  one scoped rule below), not in the `docker` group.
- `~sign/.ssh/authorized_keys`: the CI public key pinned with
  `command="/opt/sign/sign-stdin.sh",restrict`.
- `/opt/sign/sign-stdin.sh` (root:root 0755): the self-healing signer.
  `P11_KIT_SERVER_ADDRESS=unix:path=/opt/sign/p11/p11kit.sock`,
  module `/usr/lib/x86_64-linux-gnu/pkcs11/p11-kit-client.so`.
- `/opt/sign/p11/` (root 0755): the p11-kit socket dir (reachable by
  `sign`; `/run/user/1000` is not).
- `/usr/local/bin/certum-relogin` (root 0755): host wrapper —
  `docker exec`s the in-container re-login then verifies the token with
  `pkcs11-tool -L`.
- `/etc/sudoers.d/sign-relogin`: `sign ALL=(root) NOPASSWD:
  /usr/local/bin/certum-relogin` (validated, 0440).
- `certum-relogin.timer` (enabled): `OnUnitActiveSec=45min`.
- `/opt/sign/secret/{totp,userid}` (root 0600): **placeholders** —
  you fill these (see Bootstrap).

## GitHub Actions secrets

| Secret | Value |
| --- | --- |
| `SIGN_SSH_HOST` | `sign@92.222.247.17` |
| `SIGN_SSH_KEY` | The CI private key (printed once during provisioning; not stored on the VPS). |
| `SIGN_SSH_KNOWN_HOSTS` | `92.222.247.17 ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIEFkW6/XG9IdV4sPQXmEhRD1f+QuCELpDynee5DkXLrY` |
| `SIGN_SSH_PORT` | *(optional, default 22)* |

## Build & run the signing container (you run this on the VPS)

The base image (`hpvb/certum-container`) downloads and runs Certum's
closed SimplySign binary. Build it **from source on the VPS** — its
README explicitly says never to trust prebuilt images.

```bash
# base image (already cloned at ~/certum-container)
cd ~/certum-container && docker build -t certum-base .

# derived image: adds xdotool + oathtool + relogin-internal.sh
mkdir -p ~/certum-auto && cd ~/certum-auto
curl -fsSL -O https://raw.githubusercontent.com/Kenshin9977/Discord-Overlay/master/build/vps/Dockerfile
curl -fsSL -O https://raw.githubusercontent.com/Kenshin9977/Discord-Overlay/master/build/vps/relogin-internal.sh
chmod +x relogin-internal.sh
docker build -t certum-auto .

VNCPW=$(openssl rand -base64 12)
echo "VNC password (needed for the one-time bootstrap login): $VNCPW"
docker run -d --name certum-signer --restart unless-stopped \
  -e VNCPASSWORD="$VNCPW" \
  -p 127.0.0.1:5999:5900 \
  -v /opt/sign/p11:/run/p11-kit \
  -v /opt/sign/secret:/opt/sign/secret:ro \
  certum-auto
```

## Bootstrap (one time — only you can do these)

1. **Capture the TOTP secret.** Microsoft Authenticator won't reveal it
   after enrollment. Re-enroll the SimplySign 2FA in your Certum
   account and save the base32 `secret=` from the
   `otpauth://totp/...?secret=...` string. Then on the VPS:
   ```bash
   printf '%s' 'YOURBASE32SECRET'      | sudo tee /opt/sign/secret/totp   >/dev/null
   printf '%s' 'YOUR_SIMPLYSIGN_USERID'| sudo tee /opt/sign/secret/userid >/dev/null
   sudo chmod 600 /opt/sign/secret/{totp,userid}
   ```
2. **First login over VNC.** Tunnel and connect a VNC client:
   ```bash
   ssh -L 5999:127.0.0.1:5999 ubuntu@92.222.247.17   # then VNC to localhost:5999
   ```
   Enter your User ID + the OTP from the SimplySign mobile app, and
   **press the dialog's Close button** (the token is inert until you
   do). This is the only manual login ever.
3. **Calibrate the re-login once.** Run `sudo /usr/local/bin/certum-relogin`
   and check it ends with `token present`. If not, VNC in, watch the
   SimplySign login field order, adjust the xdotool block in
   `build/vps/relogin-internal.sh`, `docker build -t certum-auto`,
   recreate the container, retry. From then on the timer + self-heal
   keep it alive with no manual step.
4. **Create the GitHub secrets** above, then push a tag — the release
   is signed automatically.

## Failure modes

- **No secrets:** unsigned release, CI green. By design.
- **Session expired:** the timer normally prevents it; otherwise
  `sign-stdin.sh` self-heals (re-login + retry once).
- **Re-login broken** (Certum changed the UI, stale TOTP): retry fails,
  `sign-remote.sh` sees non‑PE/too‑small output and fails the job.
  Fail-closed. Fix `relogin-internal.sh`, rebuild, re-run.
- **VPS unreachable:** SSH step fails the job; re-run after fixing.
- **Cert reissued:** update `pkcs11:model` in `sign-stdin.sh` if it
  changes; update the thumbprint here.
