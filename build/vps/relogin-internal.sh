#!/usr/bin/env bash
# Runs INSIDE the certum-signer container (Fedora, DISPLAY=:99).
# Re-authenticates the SimplySign cloud session without a human:
# generates the current TOTP from the secret bind-mounted at
# /opt/sign/secret and types User ID + code into the SimplySign Desktop
# window with xdotool. Invoked via `docker exec` by the host's
# /usr/local/bin/certum-relogin, which does the success verification.
#
# CALIBRATION: the keystroke sequence below is a best-effort guess at
# the SimplySign Desktop 2.9.10 login dialog. It almost certainly needs
# one tuning pass: VNC in once, watch the field order, adjust the
# xdotool block, rebuild. This is the single inherently brittle spot and
# it is deliberately isolated here, off the CI build path.
set -euo pipefail
export DISPLAY=:99

secret=$(cat /opt/sign/secret/totp)
userid=$(cat /opt/sign/secret/userid)
if [[ "$secret" == REPLACE_* || "$userid" == REPLACE_* ]]; then
  echo "relogin-internal: /opt/sign/secret not populated yet" >&2
  exit 1
fi
# Certum SimplySign issues TOTP with algorithm=SHA256 (see the otpauth
# URI shown at enrollment). oathtool defaults to SHA1, which silently
# produces wrong codes here.
otp=$(oathtool --totp=sha256 -b "$secret")

# Wait for the SimplySign window to exist.
win=""
for _ in $(seq 1 30); do
  win=$(xdotool search --name 'SimplySign' 2>/dev/null | head -n1 || true)
  [[ -n "$win" ]] && break
  sleep 1
done
if [[ -z "$win" ]]; then
  echo "relogin-internal: SimplySign window not found" >&2
  exit 1
fi

xdotool windowactivate --sync "$win"
sleep 1
# ---- calibration point: default assumes User ID is focused first,
# ---- Tab moves to the OTP field, Enter submits. ----
xdotool type --delay 80 "$userid"
xdotool key Tab
xdotool type --delay 80 "$otp"
xdotool key Return
sleep 8

# The token stays inert until the post-login dialog is closed.
for w in $(xdotool search --name 'SimplySign' 2>/dev/null); do
  xdotool windowactivate --sync "$w" 2>/dev/null && xdotool key Return 2>/dev/null || true
done

echo "relogin-internal: credentials submitted" >&2
