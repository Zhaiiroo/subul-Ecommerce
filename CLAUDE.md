# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Rule files

`AGENTS.md` (root) and `.cursor/rules/*.mdc` hold the canonical, detailed conventions — the `.mdc` files are the same content re-scoped with Cursor globs. Read them for the full anti-pattern tables, and **keep them in sync when editing rules**. Workflow checklists live in `docs/ai/task-template.md` and `docs/ai/feature-workflow.md`.

Two claims in those files are now stale — trust this file instead:
- Auth **is** wired (`Program.cs` adds JWT bearer plus a `FallbackPolicy` requiring an authenticated user).
- `client/storefront/` exists; admin-panel is no longer the only frontend.

## Commands

```bash
# Backend (from repo root)
dotnet build backend/backend.csproj
dotnet run --project backend            # http://localhost:5101, Scalar docs at /scalar/v1
dotnet test backend.Tests/backend.Tests.csproj          # requires Docker (Testcontainers)
dotnet test backend.Tests/backend.Tests.csproj --filter "FullyQualifiedName~CreateCategoryHandlerTests"
dotnet test backend.Tests/backend.Tests.csproj --filter "FullyQualifiedName~CategoryFeature"

# Frontends (client/admin-panel — port 3000; client/storefront — port 3001)
npm run dev
npm run typecheck && npm run build      # the verification gate; there are no frontend tests
npm run lint
```

Full stack via Docker: `docker compose up` (postgres 17 + api + admin + storefront). Requires `POSTGRES_PASSWORD`, `JWT_SECRET`, `AUTH_SECRET` in a `.env`; the Next Dockerfiles fail the build unless `NEXT_PUBLIC_API_URL` is passed as a build arg. The storefront also reads `NEXT_PUBLIC_SITE_URL` (its own public origin) for canonical and Open Graph URLs — optional, defaulting to `http://localhost:3001`, but link previews break if it is wrong in production.

Schema changes since the Initial migration are applied by hand from `docs/sql/`; run those scripts against an existing database before starting the API. They are idempotent.

Local dev needs a Postgres matching the `DefaultConnection` in `backend/appsettings.json`, plus a `Jwt:Secret` of at least 32 chars in user-secrets or `appsettings.Development.json` — the app throws at startup otherwise. In Development, `DbSeeder` seeds data on boot.

It also needs Redis. `RedisRateLimit:Enabled` defaults to true, and startup now **fails** if it is on without a `ConnectionStrings:Redis` — that combination used to boot and enforce nothing. If Redis is configured but unreachable, `POST api/auth/login` returns 429 (it fails closed, so an outage cannot be used to strip brute-force protection) while every other route still serves. To work without Redis, set `RedisRateLimit:Enabled` to `false` in `appsettings.Development.json` — an explicit opt-out rather than a silent one.

## Architecture

```
backend/            ASP.NET Core 10 · MediatR vertical slices · EF Core · PostgreSQL
backend.Tests/      xUnit · Testcontainers Postgres · WebApplicationFactory
client/admin-panel/ Next.js 16 admin, RTL Arabic, NextAuth credentials → backend JWT
client/storefront/  Next.js 16 public store, anonymous + guest cart
DATABASE.md         51-table schema, one section per table — read only the section you need
```

Request flow: `RSC page → features/{entity} → hooks → *.api.ts → api/{entities} → {Op}Controller → ISender → Handler → Result<T> → ToActionResult() → ApiResponse<T>`

### Backend: vertical slices

Every operation is a folder `Features/{Entity}Feature/{Verb}{Entity}/` holding exactly three files: `{Op}Command.cs` (or `Query.cs` — contains the request record **and** its `Response` record), `{Op}Endpoint.cs` (file named `*Endpoint.cs`, class named `*Controller` — keep both), and `{Op}Handler.cs`. `Features/CategoryFeature/` is the template to copy; `AttributeFeature` and `ProductFeature` show larger variants.

- No repository or service layer. Handlers inject `AppDbContext` directly. Reads use `AsNoTracking()`, writes go through `SaveChangesAsync`. Controllers hold zero business logic — send and map.
- Errors are **strings**, not typed error records: `Result<T>.Failure("Category not found")`. `ResultExtensions.MapErrorToStatusCode` matches substrings — `"not found"`→404, `"unauthorized"`→401, `"already exists"`→409, everything else→400. Wrong phrasing silently yields the wrong status code.
- Business rules (uniqueness, FK existence, slug generation, delete guards) live in the handler. `FluentValidation` validators are optional shape-only checks; `ValidationBehavior` turns them into 400s with `errors[]`. `LoggingBehavior` exists but is **not** registered.
- Timestamps use `DateTime.Now` with `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)` set in both `Program.cs` and `DatabaseFixture` — never `DateTime.UtcNow`.
- `Domain/Entities/Attribute.cs` collides with `System.Attribute` and is aliased as `AttributeEntity`.
- Schema changes go in `AppDbContext.Partial.cs` (`OnModelCreatingPartial`) plus SQL and `DATABASE.md`. `AppDbContext.cs` is 2,300 lines of scaffolded output — don't read or edit it wholesale, and there is only the one Initial migration.
- `Program.cs` serves uploaded images from the repo-root `img/` directory at `/img`; `LocalImageStorageService` writes there with magic-byte validation (`ImageFileSignatures`).

### Auth model

`Program.cs` sets an authorization **fallback policy**, so every endpoint requires a valid bearer token by default. Public routes must opt out with `[AllowAnonymous]` — currently login, storefront catalog reads (category/brand/collection/product list + get-by-id/slug), all of `api/carts/*`, order creation, and guest order tracking. When adding a storefront-facing read, add `[AllowAnonymous]` or the storefront breaks; when adding an admin write, leave it off.

Admin login: `POST api/auth/login` → NextAuth Credentials provider (`client/admin-panel/auth.ts`) stores the backend `accessToken` in the JWT session; `lib/api-client.ts` attaches it as a bearer header (falling back to `auth()` during SSR), and `middleware.ts` redirects unauthenticated traffic to `/login`.

A bearer token is not trusted on its own. `AdminSessionValidator` runs from `JwtBearerEvents.OnTokenValidated` and re-reads the `admin_users` row on every authenticated request, rejecting the token when the account is inactive or when its `password_changed_at` stamp no longer matches the `pwd_stamp` claim, and refreshing the role and must-change claims from the row. That is one primary-key read per authenticated request; anonymous storefront traffic sends no token and never pays for it. The practical consequence: deactivating an account or resetting its password ends its existing sessions immediately instead of eight hours later.

`admin_users.must_change_password` closes the API to everything but `api/auth/me`, `api/auth/change-password` and `api/auth/logout` (`PasswordChangeRequiredMiddleware`, 403 with the message `Password change required`). It is set on every account the panel creates and on every admin-issued reset. `middleware.ts` redirects such a session to `/change-password`; `lib/api-client.ts` handles the 403 and 401 cases for calls already in flight.

Admin-user management lives at `api/admin-users/*`, all of it `[Authorize(Roles = "superadmin")]`, plus self-service `POST api/auth/change-password` which every role may call. Roles are `superadmin` / `manager` / `staff` (`AdminUserRoles`); password rules and the temporary-password generator are in `AdminPasswordPolicy`, mirrored by the Zod schemas in `features/admin-user/schemas/` and `features/auth/schemas/change-password.schema.ts`. Generated passwords are returned once in the create and reset responses and never again. Handler guards refuse deactivating, deleting or demoting yourself or the last active superadmin, and refuse deleting an account that is referenced by existing records.

First account: `AdminBootstrapper` runs at startup in every environment but creates an account only when `admin_users` is empty, so in Development the seeder wins and this is effectively the production path. Configure it with `AdminBootstrap:Enabled` / `Name` / `Email` / `Password`; leave the password unset and the API generates one and logs it once at Warning. It is disabled in `appsettings.Testing.json` so integration tests seed their own accounts.

### Frontend feature modules

Both clients share one layout — copy `admin-panel/features/category/` (or `storefront/features/product/`) rather than inventing structure:

```
features/{entity}/
  api/{entity}.api.ts       private Backend* interfaces + toDto transforms; gate on data.success
  api/{entity}.cached.ts    React cache() wrappers for RSC dedup (metadata + page body)
  hooks/use{Entity}.ts      TanStack Query, {entity}Keys factory, staleTime 60s
  hooks/use{Entity}Mutations.ts
  schemas/                  Zod, messages from lib/messages.ar.ts
  search-params.ts          nuqs parsers
  types/, constants/, utils/
  components/pages/         exported page components
  components/blocks/        internal pieces
  index.ts                  the only public entry — never deep-import from app/
```

`app/**/page.tsx` files are thin RSC shells: metadata plus a component imported from the `@/features/{entity}` barrel. Server side = pages, layouts, `api/`, `schemas`, `types`, `search-params`. Client side (`"use client"`) = everything under `components/` and `hooks/`. Anything shared across features belongs in `components/` or `lib/`, never a cross-feature import.

The admin panel is RTL Arabic: use logical properties (`ms-`/`me-`/`start`/`end`, never `ml-`/`mr-`), keep user-facing strings in `lib/messages.ar.ts`, and add `components/ui/` primitives with `npx shadcn@latest add` rather than by hand. A new admin entity also needs a `config/navigation.ts` `navMain` entry.

Storefront carts are guest-based: a `cart_session_id` in `localStorage` (`lib/cart-session.ts`) keys the anonymous cart, merged on login via `api/carts/merge`. Its `lib/api-client.ts` sends no auth header at all.

**Next.js 16 differs substantially from model training data** — read `node_modules/next/dist/docs/` before changing routing or data fetching in either client.

### Tests

xUnit against a real Postgres in Testcontainers, so Docker must be running. Handler tests construct the handler directly (`new {Op}Handler(fixture.CreateContext())`); integration tests use `TestWebApplicationFactory(connectionString)` with an `HttpClient`, and protected routes need `AuthTestHelper.CreateAuthenticatedClientAsync`. Every test class carries `[Collection("Database")]`. Use `Guid.NewGuid()` in names for isolation. Do not introduce `UseInMemoryDatabase` or MediatR mocks. `backend.Tests/Features/CategoryFeature/` is the template. Integration tests depend on `public partial class Program { }` at the end of `Program.cs`.

## Scanning discipline

This repo is large (51 entities, 18 backend features, two Next apps). Scope reads to one feature folder — `backend/Features/{Entity}Feature/` or `client/*/features/{entity}/` — plus `Domain/Entities/{Entity}.cs` and the single relevant `DATABASE.md` section. Never grep the whole repo, never read all 51 entities or the full `AppDbContext.cs`, and note that `frontend/` does not exist (the path is `client/`). Read the entity file before assuming field names — e.g. `Product` has `Currency` and `Status`, not `IsActive`/`IsDigital`.

API-first: get the backend endpoint building and tested before wiring the client.
