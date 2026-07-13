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
SSIGN_REV=f4fc7d7aca180982a11d9be4ee5aebd394ad5545
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

# Authenticode needs the Microsoft timestamp OID; upstream uses the generic CMS
# one, so Windows silently sees no timestamp at all. Full rationale, and the diff
# we send upstream, in ssign-authenticode-timestamp-oid.patch next to this file.
#
# Applied as a checked substitution rather than `git apply`: a patch file is at
# the mercy of CRLF checkouts and blank context lines, and a *silently* failed
# patch here ships binaries whose signatures die with the certificate. Assert
# both ends instead.
OID_FILE="$src/ssign/ssign-core/src/authenticode.rs"
OID_CMS='1.2.840.113549.1.9.16.2.14'
OID_AUTHENTICODE='1.3.6.1.4.1.311.3.3.1'

grep -q "OID_TIMESTAMP_TOKEN: &str = \"$OID_CMS\"" "$OID_FILE" || {
  echo "ERROR: expected upstream OID $OID_CMS not found at pin $SSIGN_REV." >&2
  echo "       Upstream may have fixed this. Re-read the diff before moving the pin." >&2
  exit 1
}
sed -i "s|OID_TIMESTAMP_TOKEN: &str = \"$OID_CMS\"|OID_TIMESTAMP_TOKEN: \&str = \"$OID_AUTHENTICODE\"|" "$OID_FILE"
grep -q "OID_TIMESTAMP_TOKEN: &str = \"$OID_AUTHENTICODE\"" "$OID_FILE" || {
  echo "ERROR: Authenticode timestamp OID fix did not apply." >&2
  exit 1
}
echo "applied: Authenticode timestamp OID fix ($OID_CMS -> $OID_AUTHENTICODE)"

# Fix 2 — the digest must cover the 8-byte alignment padding.
#
# ssign hashes the PE, then pads it to an 8-byte boundary before appending the
# certificate table. That padding sits INSIDE the region Windows hashes, so any
# binary whose length is not already a multiple of 8 gets a signature Windows
# reports as HashMismatch — i.e. "this file has been tampered with", which is
# worse than shipping it unsigned. It is invisible on a test file that happens to
# be 8-aligned, which is exactly how it reached a release here.
python3 - "$OID_FILE" <<'PY'
import sys
path = sys.argv[1]
src = open(path, encoding="utf-8").read()
anchor = """    if cert_end < pe.len() {
        h.update(&pe[cert_end..]);
    }
    Ok(h.finalize().into())
}"""
fixed = """    if cert_end < pe.len() {
        h.update(&pe[cert_end..]);
    }
    // An unsigned file is zero-padded to an 8-byte boundary by embed() before the
    // certificate table is appended, and that padding falls inside the region
    // Windows hashes. It must be hashed here too, or every PE whose length is not
    // already a multiple of 8 gets a digest Windows disagrees with.
    if l.cert_table_size == 0 {
        let pad = (8 - (pe.len() % 8)) % 8;
        h.update(&[0u8; 8][..pad]);
    }
    Ok(h.finalize().into())
}"""
if fixed in src:
    print("pe_hash padding fix: already present")
    sys.exit(0)
if anchor not in src:
    sys.exit("ERROR: pe_hash anchor not found — upstream changed; re-check before moving the pin.")
open(path, "w", encoding="utf-8").write(src.replace(anchor, fixed, 1))
print("applied: pe_hash 8-byte alignment padding fix")
PY

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
