# Subul runtime environments

The repository has one codebase and three isolated runtime modes. Commands are
kept explicit on purpose; no wrapper scripts are required.

## Environment matrix

| Mode | Applications | PostgreSQL and Redis | Images |
|---|---|---|---|
| Development | Native `dotnet run` / `next dev` | Docker, `subul-dev` | Repository `img/` |
| Staging | Full Docker stack | Docker, `subul-staging` | `.runtime/staging/img` |
| Production | Full Docker stack | Docker on the server | Cloudflare R2 |

Development and Staging must never share a PostgreSQL volume.

Redis runs as two password-protected instances with no persistence:

- `redis`: security state such as rate-limit counters. It uses `noeviction`,
  because an evicted key silently weakens a control.
- `redis-cache`: the output cache. It uses `allkeys-lru`, because evicting old
  entries is what a cache should do.

Local development runs only `redis`; the backend falls back to it for the cache.
Passwords go in the env file as letters and digits only, because they are
embedded in a connection string where `,` and `=` are separators.

Every container runs as a non-root user, and every container's log rotates at
10 MB × 5 files. Each environment has its own network subnet
(`SUBUL_NETWORK_SUBNET`). Traefik holds a fixed address in it (`TRAEFIK_IPV4`),
which is the only address the API trusts for `X-Forwarded-For`.

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
Copy-Item backend/appsettings.Development.example.json backend/appsettings.Development.json
Copy-Item client/admin-panel/.env.example client/admin-panel/.env.local
Copy-Item client/storefront/.env.example client/storefront/.env.local
```

Use different database names and credentials for Development and Staging.
`backend/appsettings.Development.json` is ignored by Git. Its PostgreSQL and
Redis passwords must match `.env.development`, because the native backend
connects through the published ports.

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

Apply pending migrations (needed on a new volume and after pulling a new
migration), then run the backend from another terminal:

```powershell
Set-Location backend
dotnet run -- --migrate
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

Staging URLs with the example ports (one port, routed by host name; browsers
resolve `*.localhost` to loopback without a hosts-file entry):

- Storefront: `http://localhost:8080` (API under `/api` on the same host)
- Admin: `http://admin.localhost:8080` (API under `/backend/api` on the same host)
- PostgreSQL: `127.0.0.1:5434`
- Redis: `127.0.0.1:6380`

Staging runs with `ASPNETCORE_ENVIRONMENT=Staging`; it does not run the
Development database seeder. The `migrate` service creates the schema on an
empty volume, so the stack starts without a dump, but with no catalog data;
restore a reviewed dump first if Staging needs data.

## Migrations

The schema comes only from EF Core migrations in `backend/Migrations/`. Normal
API startup never changes it. Every stack start runs the one-shot `migrate`
service first (the api image with `--migrate`), and `api` starts only after it
exits successfully. A failed migration therefore stops the deployment at that
step instead of starting the API against a half-upgraded schema. See what it did:

```bash
docker compose ... logs migrate
```

Take a verified backup before any deployment that brings a new migration.
Migrations should stay backward compatible (add columns before code depends on
them, drop them one release later), so an application rollback does not also
need a schema rollback.

Creating a migration (the `dotnet-ef` local tool; run `dotnet tool restore` once):

```powershell
dotnet ef migrations add <Name> --project backend
```

`docs/sql/` is retired. Its only script is now the idempotent
`AdminUserPasswordPolicy` migration, which is safe on databases where the script
was already applied by hand.

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

## Routing

Traefik routes by host name from `deploy/traefik/dynamic/routes.yml`, a template
filled from `STORE_HOST`, `ADMIN_HOST` and `TRAEFIK_ENTRYPOINT`:

| Host | Path | Goes to |
|---|---|---|
| `STORE_HOST` | `/api`, `/img` | api |
| `STORE_HOST` | everything else | storefront |
| `ADMIN_HOST` | `/backend/*` (prefix stripped) | api |
| `ADMIN_HOST` | everything else | admin |

Any other host, including the bare server IP, matches nothing and gets a 404.
The admin panel reaches the API under `/backend` because it serves Auth.js on
`/api/auth/*` itself.

The admin container sets `AUTH_URL` to the panel's public origin, derived from
`ADMIN_HOST`. Auth.js otherwise builds its URLs from the address the Next server
listens on (`http://0.0.0.0:3000`), and every sign-out redirected there.

Both frontends call the API on their own origin, with relative
`NEXT_PUBLIC_API_URL` values (`/api`, `/backend/api`) fixed in `compose.yaml`.
Nothing is cross-origin, so CORS is off outside local development. The API
answers only to `STORE_HOST`, `ADMIN_HOST` and `api`, the in-network name.

While rendering on the server, the storefront and admin servers call the API
from inside the network. They forward the visitor's `X-Forwarded-For`. The API
trusts that header only from the three fixed addresses (`.10` traefik,
`.11` storefront, `.12` admin in `SUBUL_NETWORK_PREFIX`), and reads only the
entry Traefik appended. Without this, every visitor would share one
rate-limit bucket, the storefront's.

## Production

Create `/etc/subul/production.env` from `.env.production.example`, restrict it
to `chmod 600`, and replace every placeholder. Never send secrets through chat
or commit them to Git.

**HTTPS.** Production mounts `deploy/traefik/traefik.production.yml`. Port 80
only redirects to 443 and answers Let's Encrypt's HTTP-01 challenge. Traefik
requests and renews a certificate for each host by itself and keeps it in the
`letsencrypt` volume. The only requirement is that both host names resolve to
the server and port 80 is reachable from the internet.

**No domain yet: sslip.io.** For server IP `a.b.c.d`, use
`STORE_HOST=a-b-c-d.sslip.io` and `ADMIN_HOST=admin.a-b-c-d.sslip.io`. These
names resolve to the IP with no registration and get real certificates.

**Moving to the domain.** Point the DNS records at the server (Cloudflare,
DNS-only, grey cloud) and change `STORE_HOST`, `ADMIN_HOST` and
`PUBLIC_SITE_URL`. Rebuild the storefront, because `PUBLIC_SITE_URL` is compiled
in for canonical and Open Graph URLs. Traefik issues the new certificates on the
first request. Nothing in the code or the routing file changes.

**Images.** Until the domain exists, `IMAGE_STORAGE_PROVIDER=Local` writes
uploads to `UPLOADS_PATH` on the host, outside Docker. Create that directory
once, owned by the api user:

```bash
sudo install -d -o 1654 -g 1654 /srv/subul/img
```

Back it up together with the database.

**Moving images to Cloudflare R2** (after the domain is on Cloudflare, because an
R2 custom domain requires a Cloudflare zone):

1. Create the bucket with a custom domain such as `media.<domain>`, and a
   bucket-scoped Object Read & Write key.
2. Upload `UPLOADS_PATH` as-is. Local `/img/products/1/x.png` and R2 key
   `products/1/x.png` use the same layout.
3. Take and verify a database backup. Then, in one reviewed transaction, rewrite
   the stored `/img/` prefix to `https://media.<domain>/` in the image columns.
4. Set `IMAGE_STORAGE_PROVIDER=R2` with the five R2 values, and rebuild the
   frontends (`PUBLIC_IMAGE_URL` is compiled into their image configuration).

The R2 values are validated at startup only when the provider is `R2`; missing
or invalid values then prevent the API from starting.

Validate before every deployment:

```bash
docker compose --env-file /etc/subul/production.env -f compose.yaml -f compose.production.yaml -p subul-prod config --quiet
```

For the current build-on-server phase:

```bash
docker volume create --label com.subul.type=database --label com.subul.environment=production subul-production-postgres
docker compose --env-file /etc/subul/production.env -f compose.yaml -f compose.production.yaml -p subul-prod up --build -d --wait
```

`up` runs `migrate` before `api`, so a new production volume gets its schema
without any manual step; `AdminBootstrapper` then creates the first account.
Only ports 80 and 443 are published. PostgreSQL, both Redis instances and the
application containers have no host port.

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
