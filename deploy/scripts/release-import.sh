#!/usr/bin/env bash
# release-import.sh — install a release archive on the server. It loads and
# links the release; it does not start or stop anything.
#
#   sudo bash release-import.sh /opt/subul/incoming/subul-release-<tag>.tar
#
# Layout it maintains:
#   /opt/subul/releases/subul-release-<tag>/   one directory per release
#   /opt/subul/current -> releases/...         the release `subul` runs
#   /usr/local/bin/subul -> current/subul
#
# Secrets never live in a release: they stay in /etc/subul/production.env,
# which every release reads, so an update never touches them.

set -euo pipefail

ARCHIVE="${1:?Usage: release-import.sh <subul-release-TAG.tar>}"
BASE="${SUBUL_HOME:-/opt/subul}"
ENV_FILE="${SUBUL_ENV_FILE:-/etc/subul/production.env}"
NAME="$(basename "$ARCHIVE" .tar)"
DIR="$BASE/releases/$NAME"

die() { echo "ERROR: $*" >&2; exit 1; }

[[ "$NAME" == subul-release-* ]] || die "expected a subul-release-<tag>.tar archive."
[[ ! -e "$DIR" ]] || die "$DIR already exists. To switch to it: sudo ln -sfn $DIR $BASE/current"

echo "SHA-256 of the archive: $(sha256sum "$ARCHIVE" | cut -d' ' -f1)"
echo "(compare it with the value release-export.sh printed on the workstation)"

echo "==> Extracting to $DIR"
mkdir -p "$BASE/releases"
tar -C "$BASE/releases" -xf "$ARCHIVE"

echo "==> Verifying file checksums"
(cd "$DIR" && sha256sum --quiet -c SHA256SUMS) || { rm -rf "$DIR"; die "checksum mismatch — the archive is damaged; copy it again."; }

echo "==> Loading images"
gunzip -c "$DIR/images.tar.gz" | docker load
# The images now live in Docker; the compressed copy is only disk space.
rm -f "$DIR/images.tar.gz"

chmod 755 "$DIR/subul" "$DIR"/deploy/scripts/*.sh
# Only an existing link names a previous release: readlink -f on a missing
# path returns the path itself, which would print a bogus rollback command.
PREVIOUS=""
[[ -L "$BASE/current" ]] && PREVIOUS="$(readlink -f "$BASE/current")"
ln -sfn "$DIR" "$BASE/current"
ln -sfn "$BASE/current/subul" /usr/local/bin/subul

echo
echo "Installed $(cat "$DIR/RELEASE_TAG"):"
sed 's/^/  /' "$DIR/RELEASE"
[[ -n "$PREVIOUS" ]] && echo "Previous release: $PREVIOUS (rollback: sudo ln -sfn $PREVIOUS $BASE/current && sudo subul up -d)"

if [[ ! -f "$ENV_FILE" ]]; then
  echo
  echo "FIRST INSTALL: create $ENV_FILE from $DIR/production.env.example"
  echo "(docs/deploy/vm-install.md, step 5), then: sudo subul up -d"
  exit 0
fi

subul config --quiet && echo "Configuration is valid."
echo
echo "Next: sudo $DIR/deploy/scripts/backup.sh && sudo subul up -d"
