#!/usr/bin/env bash
# release-export.sh — build a production release on this workstation and pack
# everything the server needs into one archive. Run from Git Bash:
#
#   bash deploy/scripts/release-export.sh            # tag = short git SHA
#   bash deploy/scripts/release-export.sh 2026.10.1  # explicit tag
#
# Produces deploy-out/subul-release-<tag>.tar containing:
#
#   images.tar.gz            subul-backend, subul-admin, subul-storefront,
#                            traefik, postgres, redis
#   compose.yaml             common stack
#   compose.production.yaml  production override (no builds, no pulls)
#   deploy/traefik/...       Traefik static + dynamic configuration
#   deploy/scripts/...       backup.sh, restore.sh, release-import.sh
#   subul                    compose wrapper for the server
#   production.env.example   template for /etc/subul/production.env
#   RELEASE, RELEASE_TAG     what was built, from which commit, with which hosts
#   SHA256SUMS               checked by release-import.sh
#
# Gates, in order: a clean working tree, no known high/critical vulnerabilities,
# the backend test suite. Escape hatches (each printed loudly): ALLOW_DIRTY=1,
# SKIP_AUDIT=1, SKIP_TESTS=1.

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

RELEASE_ENV="${RELEASE_ENV:-$ROOT/.env.release}"
THIRD_PARTY=(traefik:v3.6.1 postgres:17-alpine redis:8.10.1-alpine)

die() { echo "ERROR: $*" >&2; exit 1; }
step() { echo; echo "==> $*"; }

[[ -f "$RELEASE_ENV" ]] || die "missing $RELEASE_ENV — copy .env.release.example and set the hosts."

# A CRLF shebang fails on Linux with "bad interpreter" before a line runs.
# .gitattributes prevents it on checkout; this catches a copy edited by hand.
if grep -l $'\r' deploy/scripts/* >/dev/null 2>&1; then
  die "CRLF line endings in: $(grep -l $'\r' deploy/scripts/* | tr '\n' ' ')— the server cannot run them."
fi

if [[ -n "$(git status --porcelain)" ]]; then
  [[ "${ALLOW_DIRTY:-0}" == 1 ]] || die "uncommitted changes: the tag would not describe the code. Commit first, or ALLOW_DIRTY=1."
  echo "WARNING: building from a dirty working tree (ALLOW_DIRTY=1)."
fi

TAG="${1:-$(git rev-parse --short HEAD)}"
[[ "$TAG" =~ ^[A-Za-z0-9._-]+$ ]] || die "tag '$TAG' may contain only letters, digits, '.', '_' and '-'."
NAME="subul-release-$TAG"
OUT="$ROOT/deploy-out"
STAGE="$OUT/$NAME"
[[ ! -e "$OUT/$NAME.tar" ]] || die "$OUT/$NAME.tar already exists — pick another tag or delete it."

# Build-time environment: the production template for every required variable,
# then the real hosts, then the tag. For duplicate keys the last line wins.
BUILD_ENV="$(mktemp)"
trap 'rm -f "$BUILD_ENV"' EXIT
{ cat .env.production.example; echo; cat "$RELEASE_ENV"; echo; echo "IMAGE_TAG=$TAG"; } > "$BUILD_ENV"

for key in STORE_HOST ADMIN_HOST PUBLIC_SITE_URL; do
  value="$(grep -E "^$key=" "$RELEASE_ENV" | tail -1 | cut -d= -f2-)"
  [[ -n "$value" ]] || die "$key is empty in $RELEASE_ENV."
  [[ "$value" != *203-0-113-10* ]] || die "$key still holds the example address in $RELEASE_ENV."
done

if [[ "${SKIP_AUDIT:-0}" == 1 ]]; then
  echo "WARNING: dependency audit skipped (SKIP_AUDIT=1)."
else
  step "Checking dependencies for known vulnerabilities"
  report="$(dotnet list backend/backend.csproj package --vulnerable --include-transitive 2>&1)"
  echo "$report" | tail -3
  if echo "$report" | grep -qiE '\b(high|critical)\b'; then die "vulnerable NuGet packages (see above)."; fi
  (cd client/storefront && npm audit --omit=dev --audit-level=high)
  (cd client/admin-panel && npm audit --omit=dev --audit-level=high)
fi

if [[ "${SKIP_TESTS:-0}" == 1 ]]; then
  echo "WARNING: backend tests skipped (SKIP_TESTS=1)."
else
  step "Running the backend test suite (needs Docker)"
  dotnet test backend.Tests/backend.Tests.csproj --nologo --verbosity quiet
fi

step "Building subul-backend, subul-admin and subul-storefront :$TAG"
# compose.yaml alone: compose.production.yaml removes the build sections so the
# server can never build, and so cannot be used for building either.
docker compose --env-file "$BUILD_ENV" -f compose.yaml -p subul-release build api admin storefront

step "Ensuring the third-party images are present"
for image in "${THIRD_PARTY[@]}"; do
  docker image inspect "$image" >/dev/null 2>&1 || docker pull "$image"
done

step "Assembling $NAME"
rm -rf "$STAGE"
mkdir -p "$STAGE/deploy/traefik/dynamic" "$STAGE/deploy/scripts"
cp compose.yaml compose.production.yaml "$STAGE/"
cp deploy/traefik/traefik.yml deploy/traefik/traefik.production.yml "$STAGE/deploy/traefik/"
cp deploy/traefik/dynamic/*.yml "$STAGE/deploy/traefik/dynamic/"
cp deploy/scripts/backup.sh deploy/scripts/restore.sh deploy/scripts/release-import.sh "$STAGE/deploy/scripts/"
cp deploy/scripts/subul "$STAGE/subul"
cp .env.production.example "$STAGE/production.env.example"
echo "$TAG" > "$STAGE/RELEASE_TAG"
{
  echo "tag:        $TAG"
  echo "commit:     $(git rev-parse HEAD)"
  echo "built:      $(date '+%Y-%m-%d %H:%M:%S %z')"
  echo "compiled in:"
  grep -E '^(STORE_HOST|ADMIN_HOST|PUBLIC_SITE_URL|PUBLIC_IMAGE_URL)=' "$RELEASE_ENV" | sed 's/^/  /'
} > "$STAGE/RELEASE"

step "Saving images (this takes a while)"
docker save "subul-backend:$TAG" "subul-admin:$TAG" "subul-storefront:$TAG" "${THIRD_PARTY[@]}" \
  | gzip -1 > "$STAGE/images.tar.gz"

step "Validating the bundle as the server will see it"
# Same --env-file shape as the server, from the bundle's own files only.
{ cat "$STAGE/production.env.example"; echo; cat "$RELEASE_ENV"; } > "$BUILD_ENV"
IMAGE_TAG="$TAG" docker compose --project-directory "$STAGE" --env-file "$BUILD_ENV" \
  -f "$STAGE/compose.yaml" -f "$STAGE/compose.production.yaml" -p subul-prod config --quiet

(cd "$STAGE" && find . -type f ! -name SHA256SUMS -print0 | sort -z | xargs -0 sha256sum > SHA256SUMS)
tar -C "$OUT" -cf "$OUT/$NAME.tar" "$NAME"
rm -rf "$STAGE"

echo
echo "Done: deploy-out/$NAME.tar ($(du -h "$OUT/$NAME.tar" | cut -f1))"
echo "SHA-256: $(sha256sum "$OUT/$NAME.tar" | cut -d' ' -f1)"
echo "Copy it to the server (scp or FileZilla) into /opt/subul/incoming/, then follow"
echo "docs/deploy/vm-install.md — release-import.sh prints the same SHA-256 to compare."
