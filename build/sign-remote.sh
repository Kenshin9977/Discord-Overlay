#!/usr/bin/env bash
# Sign a single Windows binary by delegating to the Certum SimplySign
# signing host (the Linux VPS). Invoked once per file by Velopack via
#   vpk pack --signTemplate 'bash ./build/sign-remote.sh "{{file}}"'
#
# The file is streamed over SSH to a locked-down forced-command account
# on the VPS, which runs osslsigncode against a persistent SimplySign
# p11-kit session and streams the signed binary back. Nothing about the
# certificate, the TOTP, or the cloud session ever touches CI.
#
# Required environment (wired from GitHub secrets in release.yml):
#   SIGN_SSH_HOST        user@host of the signing account on the VPS
#   SIGN_SSH_KEY_FILE    path to the private key authorised for that account
# Optional:
#   SIGN_SSH_PORT        SSH port (default 22)
#   SIGN_SSH_KNOWN_HOSTS path to a known_hosts file pinning the VPS host
#                        key. Strongly recommended; without it the host
#                        key is trusted on first use.
set -euo pipefail

file=${1:?usage: sign-remote.sh <file>}

# vpk substitutes {{file}} with the path ALREADY wrapped in double quotes. The
# template therefore quotes it with single quotes — double quotes would make bash
# read the Windows separators as escapes and silently eat them, turning
# C:\Users\...\App.exe into C:UsersApp.exe. The cost is that the quotes vpk added
# arrive here literally, so strip that one layer.
file="${file#\"}"
file="${file%\"}"

: "${SIGN_SSH_HOST:?SIGN_SSH_HOST is required}"
: "${SIGN_SSH_KEY_FILE:?SIGN_SSH_KEY_FILE is required}"
port=${SIGN_SSH_PORT:-22}

if [[ ! -s "$file" ]]; then
  echo "sign-remote: input file missing or empty: $file" >&2
  exit 1
fi

ssh_opts=(-T -p "$port" -i "$SIGN_SSH_KEY_FILE"
          -o BatchMode=yes -o ConnectTimeout=20 -o ServerAliveInterval=15)
if [[ -n "${SIGN_SSH_KNOWN_HOSTS:-}" ]]; then
  ssh_opts+=(-o StrictHostKeyChecking=yes -o UserKnownHostsFile="$SIGN_SSH_KNOWN_HOSTS")
else
  ssh_opts+=(-o StrictHostKeyChecking=accept-new)
fi

tmp=$(mktemp "${file}.signed.XXXXXX")
trap 'rm -f "$tmp"' EXIT

echo "sign-remote: signing $(basename "$file") via $SIGN_SSH_HOST" >&2
# Forced command on the VPS ignores any argument; stdin -> signed stdout.
if ! ssh "${ssh_opts[@]}" "$SIGN_SSH_HOST" < "$file" > "$tmp"; then
  echo "sign-remote: SSH signing call failed for $file" >&2
  exit 1
fi

# A signed PE is still a PE and is larger than the input (the signature
# is appended). Cheap sanity checks so a silent failure can't ship an
# unsigned-but-renamed file.
if [[ ! -s "$tmp" ]]; then
  echo "sign-remote: signer returned no data for $file" >&2
  exit 1
fi
if [[ $(head -c 2 "$tmp") != "MZ" ]]; then
  echo "sign-remote: signer output is not a PE binary (login/session expired on the VPS?)" >&2
  exit 1
fi
if [[ $(stat -c%s "$tmp") -le $(stat -c%s "$file") ]]; then
  echo "sign-remote: signed output not larger than input — signature likely not applied" >&2
  exit 1
fi

mv -f "$tmp" "$file"
trap - EXIT
echo "sign-remote: $(basename "$file") signed" >&2
