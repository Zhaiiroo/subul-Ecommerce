# Subul runtime environments

The repository has one codebase and three isolated runtime modes. Commands are
kept explicit on purpose; no wrapper scripts are required.

## Environment matrix

| Mode | Applications | PostgreSQL and Redis | Images |
|---|---|---|---|
| Development | Native `dotnet run` / `next dev` | Docker, `subul-dev` | Repository `img/` |
| Staging | Full Docker stack | Docker, `subul-staging` | `.runtime/staging/img` |
| Production | Full Docker stack | Docker on the server | Cloudflare R2 |

Development and Staging must never share a PostgreSQL volume. Redis is an
ephemeral cache/rate-limit store and intentionally has no persistent volume.

## Files

- `compose.yaml`: common service definitions; it does not publish database or
  Redis ports and cannot select an environment by itself.
- `compose.local.yaml`: local-only ports, builds, database backup mount, and
  local image storage.
- `compose.production.yaml`: public ports, mandatory production secrets, and
  Cloudflare R2 configuration.
- `.env.development`: real local Development infrastructure values; ignored by Git.
- `.env.staging`: real local Staging values; ignored by Git.
- `/etc/subul/production.env`: real server values; never stored in the repository.
- `.env.*.example`: committed, non-secret templates.

Always pass `--env-file` explicitly. The legacy root `.env` is not part of the
new workflow, and a plain `docker compose up` is intentionally not a supported
command.

## One-time local setup

Copy the example files if the real files do not exist:

```powershell
Copy-Item .env.development.example .env.development
Copy-Item .env.staging.example .env.staging
Copy-Item client/admin-panel/.env.example client/admin-panel/.env.local
Copy-Item client/storefront/.env.example client/storefront/.env.local
```

Use different database names and credentials for Development and Staging. The
Development values in `backend/appsettings.Development.json` must match
`.env.development`, because the native backend connects through the published
PostgreSQL port.

Create the external database volumes once:

```powershell
docker volume create --label com.subul.type=database --label com.subul.environment=development subul-development-postgres
docker volume create --label com.subul.type=database --label com.subul.environment=staging subul-staging-postgres
```

This workstation already had Development data in `subul-ecommerce_pgdata`, so
its local `.env.development` deliberately references that existing volume. Do
not rename, delete, or copy the raw PostgreSQL data directory.

## Development

Start only PostgreSQL and Redis:

```powershell
docker compose --env-file .env.development -f compose.yaml -f compose.local.yaml -p subul-dev up -d --wait db redis
```

Run the backend from another terminal:

```powershell
Set-Location backend
dotnet run
```

Run the admin panel:

```powershell
Set-Location client/admin-panel
npm run dev
```

Run the storefront:

```powershell
Set-Location client/storefront
npm run dev -- --port 3001
```

Development URLs:

- API: `http://localhost:5101/api`
- Scalar: `http://localhost:5101/scalar/v1`
- Admin: `http://localhost:3000`
- Storefront: `http://localhost:3001`
- PostgreSQL: `127.0.0.1:5433`
- Redis: `127.0.0.1:6379`

## Staging

Validate the merged configuration without printing resolved secrets:

```powershell
docker compose --env-file .env.staging -f compose.yaml -f compose.local.yaml -p subul-staging config --quiet
```

Build and start the complete stack:

```powershell
docker compose --env-file .env.staging -f compose.yaml -f compose.local.yaml -p subul-staging up --build -d --wait
```

Staging URLs with the example ports:

- Storefront and API: `http://localhost:8080`
- Admin: `http://localhost:3100`
- PostgreSQL: `127.0.0.1:5434`
- Redis: `127.0.0.1:6380`

Staging runs with `ASPNETCORE_ENVIRONMENT=Staging`; it does not run the
Development database seeder. An empty Staging database must be restored from a
reviewed dump before starting the application services.

## Manual database backup

`compose.local.yaml` mounts `./backups/database` at `/backups` in PostgreSQL.
The directory is ignored by Git.

Create a custom-format backup:

```powershell
docker compose --env-file .env.development -f compose.yaml -f compose.local.yaml -p subul-dev exec -T db pg_dump -U subul -d subul-Ecommerce -Fc -f /backups/subul-development.dump
```

Verify that PostgreSQL can read the archive:

```powershell
docker compose --env-file .env.development -f compose.yaml -f compose.local.yaml -p subul-dev exec -T db pg_restore -l /backups/subul-development.dump
```

For a new empty Staging volume, start `db` and `redis`, then restore:

```powershell
docker compose --env-file .env.staging -f compose.yaml -f compose.local.yaml -p subul-staging up -d --wait db redis
docker compose --env-file .env.staging -f compose.yaml -f compose.local.yaml -p subul-staging exec -T db pg_restore -U subul -d subul-Staging --no-owner --no-privileges /backups/subul-development.dump
```

Never test a restore over the original database. Restore into a separate empty
database or disposable Staging volume and verify it there first.

## Database safety

PostgreSQL uses an external named volume. Docker Compose therefore cannot
remove it with `docker compose down -v`. The following operations can still
destroy data and must not be used without a verified backup and an exact target:

```text
docker volume rm <database-volume>
docker volume prune -a
docker system prune --volumes
Docker Desktop: Clean / Purge data
Docker Desktop: Reset
```

Safe daily commands are `stop`, `start`, `up -d`, and `down` without `-v`.
An external volume is an accident guard, not a backup. Production dumps must be
copied off the server; use a private backup bucket, never the public media bucket.

## Production and Cloudflare R2

Create `/etc/subul/production.env` from `.env.production.example`, restrict its
filesystem permissions, and replace every placeholder. Do not send R2 secrets
through chat or commit them to Git.

The checked-in Traefik routes currently serve plain HTTP. Port 443 is reserved,
but no TLS router or certificate resolver is active until the final storefront
and admin domains are known. Do not expose this stack publicly as-is: terminate
TLS in the server/platform ingress, or finish the Traefik DNS/ACME configuration
before directing production traffic to it.

Required R2 values:

- S3 service URL: `https://<ACCOUNT_ID>.r2.cloudflarestorage.com` (or the
  jurisdiction-specific endpoint).
- Bucket name.
- Bucket-scoped Object Read & Write access key ID and secret access key.
- Public custom domain such as `https://media.example.com`.

The backend uploads and deletes objects through the S3-compatible API. It stores
the public absolute URL in the database. The bucket custom domain serves image
reads; `r2.dev` is not intended for production traffic.

Existing `/img/...` records are intentionally not migrated automatically. Before
the first production cut-over, upload the existing files to R2, take and verify a
database backup, preview the affected rows, and then update those URLs to the R2
custom domain in one reviewed transaction. This must be performed only after the
bucket and final public domain exist; guessing either value now would risk broken
image references.

Validate before every deployment:

```bash
docker compose --env-file /etc/subul/production.env -f compose.yaml -f compose.production.yaml -p subul-prod config --quiet
```

For the current build-on-server phase:

```bash
docker volume create --label com.subul.type=database --label com.subul.environment=production subul-production-postgres
docker compose --env-file /etc/subul/production.env -f compose.yaml -f compose.production.yaml -p subul-prod up --build -d --wait
```

PostgreSQL and Redis are not published by `compose.production.yaml`. Production
R2 configuration is validated at application startup; missing or invalid URLs,
bucket, or credentials prevent the API from starting.

## Verification

```powershell
Set-Location backend
dotnet build
dotnet test ../backend.Tests/backend.Tests.csproj

Set-Location ../client/admin-panel
npm run typecheck
npm run build

Set-Location ../storefront
npm run typecheck
npm run build
```
