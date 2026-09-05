#!/usr/bin/env bash
# Build and install the signing host's `ssign` binary + forced-command signer.
# Run on the VPS as a user with sudo. Idempotent.
#
#   curl -fsSL https://raw.githubusercontent.com/Kenshin9977/Discord-Overlay/master/build/vps/install-signer.sh | bash
#
# Pinned to a specific upstream commit on purpose: ssign is a young, single-author
# project that sits on the path to our signing key. A pinned SHA cannot be swapped
# out from under us by a force-push — the build breaks instead of silently changing.
# Re-read the diff before moving the pin.
set -euo pipefail

SSIGN_REPO=https://github.com/Le-Syl21/ssign
# v0.1.5. Carries both Authenticode fixes this repo used to patch in locally
# (timestamp OID, alignment padding), plus bounds checks on the certificate
# table and a digest-matched signature response.
SSIGN_REV=dbe67515789760bbb93499007723daf8048d4d4e
RAW=https://raw.githubusercontent.com/Kenshin9977/Discord-Overlay/master/build/vps

command -v cargo >/dev/null 2>&1 || {
  echo "installing rust toolchain…"
  curl -fsSL --proto '=https' --tlsv1.2 https://sh.rustup.rs \
    | sh -s -- -y --profile minimal --default-toolchain stable >/dev/null
}
# shellcheck disable=SC1091
. "$HOME/.cargo/env"

src=$(mktemp -d)
trap 'rm -rf "$src"' EXIT
git clone -q "$SSIGN_REPO" "$src/ssign"
git -C "$src/ssign" checkout -q "$SSIGN_REV"

# Two Authenticode bugs cost a great deal to find here and both ship binaries
# that look fine until they are not — see docs/SIGNING.md. They are fixed
# upstream now, so there is nothing left to patch, but a pin move that lost
# either one would be silent. Assert them instead, before spending a build.
AUTH="$src/ssign/ssign-core/src/authenticode.rs"

grep -q 'OID_TIMESTAMP_TOKEN: &str = "1.3.6.1.4.1.311.3.3.1"' "$AUTH" || {
  echo "ERROR: the RFC3161 token is not embedded under the Authenticode OID at $SSIGN_REV." >&2
  echo "       Signatures would carry a timestamp Windows cannot see. See docs/SIGNING.md." >&2
  exit 1
}
grep -q 'let pad = (8 - (pe.len() % 8)) % 8;' "$AUTH" || {
  echo "ERROR: pe_hash does not cover the 8-byte alignment padding at $SSIGN_REV." >&2
  echo "       Any PE whose length is not a multiple of 8 would verify as tampered with." >&2
  exit 1
}
echo "verified: Authenticode timestamp OID + pe_hash padding fixes present upstream"

( cd "$src/ssign" && cargo build --release --locked --bin ssign )

sudo install -D -o root -g root -m 0755 "$src/ssign/target/release/ssign" /opt/sign/bin/ssign

curl -fsSL -o "$src/sign-stdin.sh" "$RAW/sign-stdin.sh"
sudo install -D -o root -g root -m 0755 "$src/sign-stdin.sh" /opt/sign/sign-stdin.sh

# State files the signer writes (it runs as the unprivileged `sign` user).
sudo touch /opt/sign/.sign.lock /opt/sign/.last-totp-window
sudo chown sign:sign /opt/sign/.sign.lock /opt/sign/.last-totp-window
sudo chmod 0644 /opt/sign/.sign.lock /opt/sign/.last-totp-window

# The secrets stay root-owned and are readable by the `sign` group only. The
# directory needs 0710 — traverse, but not list: without the execute bit `sign`
# cannot open the files at all no matter how the files themselves are chmod'ed,
# and withholding read keeps it from enumerating what else lives there.
sudo chown root:sign /opt/sign/secret /opt/sign/secret/userid /opt/sign/secret/totp
sudo chmod 0710 /opt/sign/secret
sudo chmod 0640 /opt/sign/secret/userid /opt/sign/secret/totp

echo
echo "installed:"
/opt/sign/bin/ssign --version
echo "  /opt/sign/sign-stdin.sh (forced command)"
