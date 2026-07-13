#!/usr/bin/env bash
# Installed on the signing VPS as /opt/sign/sign-stdin.sh, and pinned as the
# forced command of the restricted `sign` SSH account:
#
#   command="/opt/sign/sign-stdin.sh",restrict ssh-ed25519 AAAA...
#
# CI pipes an unsigned Windows binary in on stdin and reads the signed binary
# back on stdout. That is the entire capability the key grants: no shell, no
# arguments, no file paths. The Certum TOTP seed never leaves this host.
#
# Signing goes straight to Certum's cloud over HTTPS via `ssign`. There is no
# SimplySign Desktop, no container, no X server, no PKCS#11 — see docs/SIGNING.md.
set -euo pipefail

SSIGN=/opt/sign/bin/ssign
SECRET_DIR=/opt/sign/secret
LOCK=/opt/sign/.sign.lock
WINDOW_STATE=/opt/sign/.last-totp-window
TOTP_PERIOD=30
MAX_ATTEMPTS=4

log() { echo "sign-stdin: $*" >&2; }

# Certum rate-limits logins per TOTP code: two in the same 30s window succeed,
# a third is rejected. A release signs 3-4 files, each its own invocation, so
# without this the pipeline would fail at random. Serialise on a lock and spend
# at most one login per TOTP window — deterministic, and it never burns a failed
# auth attempt against the account (repeated failures risk a lockout).
exec 9>"$LOCK"
flock 9

wait_for_fresh_window() {
  local last now
  last=$(cat "$WINDOW_STATE" 2>/dev/null || echo 0)
  now=$(( $(date +%s) / TOTP_PERIOD ))
  while [[ "$now" == "$last" ]]; do
    sleep $(( TOTP_PERIOD - ($(date +%s) % TOTP_PERIOD) + 1 ))
    now=$(( $(date +%s) / TOTP_PERIOD ))
  done
  echo "$now" > "$WINDOW_STATE"
}

in=$(mktemp /tmp/sign.XXXXXX.exe)
trap 'rm -f "$in"' EXIT
cat > "$in"

if [[ ! -s "$in" ]]; then
  log "no input on stdin"
  exit 1
fi
if [[ $(head -c2 "$in") != "MZ" ]]; then
  log "input is not a PE binary"
  exit 1
fi
before=$(stat -c%s "$in")

email=$(tr -d '[:space:]' < "$SECRET_DIR/userid")
# The full otpauth:// URI, not a bare secret: it carries algorithm/digits/period,
# so nothing here has to guess them. (Certum is SHA-256 — the default SHA-1 of
# most TOTP tools, and of Microsoft Authenticator, produces codes it rejects.)
seed=$(cat "$SECRET_DIR/totp")

for attempt in $(seq 1 "$MAX_ATTEMPTS"); do
  wait_for_fresh_window
  if "$SSIGN" --email "$email" --otp "$seed" "$in" >&2; then
    after=$(stat -c%s "$in")
    if (( after <= before )); then
      log "signed output is not larger than input — signature not applied"
      exit 1
    fi
    log "signed ($before -> $after bytes)"
    cat "$in"
    exit 0
  fi
  log "sign attempt $attempt/$MAX_ATTEMPTS failed; retrying in the next TOTP window"
done

# Fail closed: never hand back an unsigned binary as if it were signed.
log "giving up after $MAX_ATTEMPTS attempts"
exit 1
