#!/usr/bin/env bash
# backup.sh — back up the production database and the uploaded images.
#
#   sudo /opt/subul/current/deploy/scripts/backup.sh
#
# Run by cron every night (docs/deploy/vm-install.md) and by hand before every
# update. Writes to DATABASE_BACKUP_PATH (from /etc/subul/production.env):
#
#   subul-db-<stamp>.dump      pg_dump custom format, verified with pg_restore -l
#   subul-img-<stamp>.tar.gz   UPLOADS_PATH (only while images are stored locally)
#
# Keeps KEEP_DAYS days (default 14). The files hold customers' names, phones
# and addresses, so they are readable by root only.
#
# A backup on the same disk is not a backup of the server. With
# BACKUP_OFFSITE_REMOTE set (an rclone remote such as r2-backups:subul, pointing
# at a PRIVATE bucket — never the public media one) each run copies the new
# files there; until then, download them regularly (docs, "Backups").

set -euo pipefail

ENV_FILE="${SUBUL_ENV_FILE:-/etc/subul/production.env}"
SUBUL="${SUBUL:-/usr/local/bin/subul}"
KEEP_DAYS="${KEEP_DAYS:-14}"

die() { echo "$(date '+%F %T') ERROR: $*" >&2; exit 1; }
log() { echo "$(date '+%F %T') $*"; }
env_value() { grep -E "^$1=" "$ENV_FILE" | tail -1 | cut -d= -f2- || true; }

[[ -r "$ENV_FILE" ]] || die "cannot read $ENV_FILE."
BACKUP_DIR="$(env_value DATABASE_BACKUP_PATH)"
UPLOADS="$(env_value UPLOADS_PATH)"
OFFSITE="$(env_value BACKUP_OFFSITE_REMOTE)"
[[ -d "$BACKUP_DIR" ]] || die "DATABASE_BACKUP_PATH ($BACKUP_DIR) does not exist."

umask 077
STAMP="$(date +%Y%m%d-%H%M%S)"
DUMP="subul-db-$STAMP.dump"
IMAGES="subul-img-$STAMP.tar.gz"

log "Dumping the database to $BACKUP_DIR/$DUMP"
# The db container mounts DATABASE_BACKUP_PATH at /backups; its own
# POSTGRES_USER/POSTGRES_DB are used, so no credential passes through here.
# </dev/null on every exec: compose would otherwise read this script's stdin,
# swallowing whatever a caller piped in after it.
"$SUBUL" exec -T db sh -c "pg_dump -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" -Fc -f /backups/$DUMP" </dev/null
"$SUBUL" exec -T db pg_restore -l "/backups/$DUMP" </dev/null >/dev/null || die "the new dump cannot be read by pg_restore."
chmod 600 "$BACKUP_DIR/$DUMP"
log "Database backup verified ($(du -h "$BACKUP_DIR/$DUMP" | cut -f1))."

if [[ -n "$UPLOADS" && -d "$UPLOADS" ]]; then
  tar -C "$UPLOADS" -czf "$BACKUP_DIR/$IMAGES" .
  chmod 600 "$BACKUP_DIR/$IMAGES"
  log "Images archived ($(du -h "$BACKUP_DIR/$IMAGES" | cut -f1))."
fi

deleted="$(find "$BACKUP_DIR" -maxdepth 1 -type f -name 'subul-*' -mtime +"$KEEP_DAYS" -print -delete | wc -l)"
[[ "$deleted" -eq 0 ]] || log "Removed $deleted backup file(s) older than $KEEP_DAYS days."

if [[ -n "$OFFSITE" ]]; then
  command -v rclone >/dev/null || die "BACKUP_OFFSITE_REMOTE is set but rclone is not installed."
  rclone copy "$BACKUP_DIR" "$OFFSITE" --include "subul-*-$STAMP.*"
  log "Copied off the server to $OFFSITE."
else
  log "WARNING: no off-server copy (BACKUP_OFFSITE_REMOTE is empty). Download $BACKUP_DIR regularly."
fi

log "Backup complete."
