#!/usr/bin/env bash
# restore.sh — replace the production database with a backup. DESTRUCTIVE.
#
#   sudo /opt/subul/current/deploy/scripts/restore.sh subul-db-<stamp>.dump
#
# The dump must be in DATABASE_BACKUP_PATH (the db container sees it as
# /backups/<name>). Before touching anything this takes a fresh backup of the
# current database, so a restore can itself be undone.
#
# Order: stop the applications, restore, start again. `subul up -d` runs the
# migrate service first, so a dump from an older release is brought up to the
# current schema automatically.
#
# Images are not part of this: extract the matching subul-img-<stamp>.tar.gz
# into UPLOADS_PATH by hand if they are needed too.

set -euo pipefail

NAME="$(basename "${1:?Usage: restore.sh <subul-db-STAMP.dump>}")"
ENV_FILE="${SUBUL_ENV_FILE:-/etc/subul/production.env}"
SUBUL="${SUBUL:-/usr/local/bin/subul}"
HERE="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"

die() { echo "ERROR: $*" >&2; exit 1; }
env_value() { grep -E "^$1=" "$ENV_FILE" | tail -1 | cut -d= -f2- || true; }

BACKUP_DIR="$(env_value DATABASE_BACKUP_PATH)"
[[ -f "$BACKUP_DIR/$NAME" ]] || die "$BACKUP_DIR/$NAME not found."
# </dev/null on every exec: compose would otherwise read this script's stdin —
# here, the confirmation typed (or piped) below.
"$SUBUL" exec -T db pg_restore -l "/backups/$NAME" </dev/null >/dev/null || die "$NAME is not a readable pg_dump archive."

echo "This REPLACES the entire production database with $NAME."
echo "Every order, product and account changed since that backup will be lost."
read -r -p "Type the word restore to continue: " answer
[[ "$answer" == restore ]] || die "cancelled."

echo "==> Safety backup of the current database"
"$HERE/backup.sh"

echo "==> Stopping the applications"
"$SUBUL" stop api admin storefront

echo "==> Restoring $NAME"
"$SUBUL" exec -T db sh -c \
  "pg_restore -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" --clean --if-exists --no-owner --no-privileges /backups/$NAME" </dev/null

echo "==> Starting again (migrate runs first)"
"$SUBUL" up -d

echo "Restore complete. Check: sudo subul ps && sudo subul logs migrate"
