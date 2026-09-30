# Subul-Ecommerce

Canonical agent rules for **Cursor**, **Antigravity CLI (`agy`)**, and other AI tools.
Keep in sync with `.cursor/rules/*.mdc` when editing rules (same content, Cursor globs).

**Workflows:** `docs/ai/task-template.md` → `docs/ai/feature-workflow.md`

**Admin-panel opened as workspace root?** Use monorepo-root `../../AGENTS.md` and `../../.cursor/rules/` for full rules; local workflows in `docs/ai/` if copied there.

---

# Architecture

```
backend/ + backend.Tests/     ASP.NET Core 10, MediatR, EF Core, PostgreSQL
client/admin-panel/           Next.js 16 admin (RTL) — only frontend
DATABASE.md                   one table section per read
docs/ai/                      task-template.md, feature-workflow.md
```

**Flow:** `RSC page → features/{entity} (client) → hooks → *.api.ts → api/{entities} → Handler → Result<T> → ApiResponse<T>`

**Naming:** `{Entity}Feature` / `{Verb}{Entity}` / `api/{plural}` / `{Op}Endpoint.cs`+`{Op}Controller` / `features/{entity}/` / `{Entity}ListingPage`

## Scan Limits

| Task | Read only |
|------|-----------|
| New backend CRUD | `CategoryFeature/` (5 ops), `Domain/Entities/{Entity}.cs`, one `DATABASE.md` section |
| Edit backend op | 3 files in `Features/{Entity}Feature/{Op}/` |
| New/edit frontend | `client/admin-panel/features/category/` or `features/{entity}/` |
| Backend tests | Mirror `backend.Tests/Features/CategoryFeature/` |
| DB config | `AppDbContext.Partial.cs` |

**Never:** 51 entities, full `AppDbContext.cs`, unrelated features, `frontend/` (wrong path), `bin/`/`obj/`, `.next/`, all `components/ui/`

## Anti-Patterns

| Don't | Do |
|-------|-----|
| Search `frontend/` or entire repo | Grep within one `Features/{Entity}Feature/` or `features/{entity}/` |
| Read/guess entity fields | Read `Domain/Entities/{Entity}.cs` first |
| Build frontend before backend API | API-first |
| Repository/service layer | Handler → `AppDbContext` direct |
| Edit `AppDbContext.cs` / hand-applied SQL scripts | `AppDbContext.Partial.cs` + `dotnet ef migrations add` + `DATABASE.md` |
| `Result.Failure` typed `Error` record / throw business errors | `Result.Failure("… not found")` string |
| Wrong failure phrases | `"not found"`→404, `"already exists"`→409 |
| `DateTime.UtcNow` | `DateTime.Now` + Npgsql legacy timestamp switch |
| Assume JWT/`[Authorize]`/`FluentValidation` on Category | Auth not wired; Category validates inline in handlers |
| Rename `*Endpoint` file or `*Controller` class | Keep both names |
| `UseInMemoryDatabase` / mock MediatR in tests | Testcontainers `DatabaseFixture` |
| Register `LoggingBehavior` | Not in DI |
| Query `Attribute` entity | DbSet alias `AttributeEntity` |
| Extract shared DTOs before 3+ features | Duplicate like Category |
| Import `features/x/hooks/...` from pages | `@/features/x` barrel only |
| Cross-import between features | Shared → `components/` or `lib/` |
| Inline Arabic / duplicate backend rules in UI | `messages.ar.ts` + show API errors |
| Edit `components/ui/` by hand | `npx shadcn@latest add` |
| `ml-`/`mr-` in RTL app | `ms-`/`me-`/`start`/`end` |
| `Product.IsActive`/`IsDigital` guesses | `Product` needs `Currency`, `Status` |

**Workflow:** `docs/ai/task-template.md` → `docs/ai/feature-workflow.md`. **Sections:** Security, Backend, Frontend, Testing (below).

---

# Security

**Auth:** JWT package installed; no `UseAuthentication()`; `[Authorize]` unused — all routes public. Wire JWT in `Program.cs` before adding auth. Keep `api/carts/*` + guest order track `[AllowAnonymous]`.

**Secrets (never commit):** `.env*`, `appsettings.*.local.json`, `secrets.json`, `*.pem`/`*.key`. `NEXT_PUBLIC_*` is client-visible — API URL only, no tokens.

**Input:** Backend — Command/Query records + EF LINQ only. Frontend — Zod on forms, nuqs typed parsers, slug regex `^[a-z0-9]+(?:-[a-z0-9]+)*$`.

**Responses:** No `PasswordHash`/tokens in API. 500 → generic message (`ExceptionHandlingMiddleware`). No `dangerouslySetInnerHTML`. JWT header → `lib/api-client.ts` when auth lands.

**CI:** `.github/workflows/ci.yml` — CodeQL, vulnerable packages, Gitleaks.

---

# Backend

**Applies to:** `backend/**/*`

**Template:** `Features/CategoryFeature/`. **Namespace:** `backend`. Copy 3 files per op; swap `Category`→`{Entity}`.

| Op | HTTP | Route |
|----|------|-------|
| List{Entity}Paginated | GET | `api/{entities}` |
| Create{Entity} | POST | `api/{entities}` → 201 |
| GetById{Entity} | GET | `api/{entities}/{id:long}` |
| Update{Entity} | PUT | `api/{entities}/{id:long}` |
| Delete{Entity} | DELETE | `api/{entities}/{id:long}` |

Ref: `CreateCategoryEndpoint.cs`, `UpdateCategoryEndpoint.cs` (Request→Command+route id), `DeleteCategoryHandler.cs` (FK guards).

## Controller

`[ApiController]` `[Route("api/…")]` `[Tags]` · ctor `ISender` · `sender.Send` → `ToActionResult()` (201 on POST) · zero business logic.

## Handler

`IRequestHandler<T, Result<TResponse>>` + `AppDbContext` · reads `AsNoTracking()` · writes tracked + `SaveChangesAsync` · `Trim()`/`ToLower()` · list defaults Page=1 Limit=10 · search NameEn+NameAr · `DateTime.Now`.

**Domain rules in handler** (uniqueness, FK, slug, delete guards). Shape validation → `AbstractValidator<T>` only when needed (`ValidationBehavior` → 400 `errors[]`); none exist for Category yet.

**Records:** Command/Query + Response same file; Update has separate `Update{Entity}Request`.

## Verify

`cd backend && dotnet build && dotnet test ../backend.Tests/backend.Tests.csproj`

---

# Frontend

**Applies to:** `client/admin-panel/**/*`

**Template:** `features/category/`. **API:** `NEXT_PUBLIC_API_URL` → `lib/api-client.ts`.

Next.js 16 differs from training data — read `node_modules/next/dist/docs/` before routing/data changes.

## Split

| Server | Client (`"use client"`) |
|--------|-------------------------|
| `app/**/page.tsx`, layouts | `features/**/components/**`, `hooks/**` |
| `features/**/api`, `schemas`, `types`, `search-params.ts` | `app-providers`, sidebar, kbar |

Routes: metadata + optional SSR prefetch → import page component from `@/features/{entity}` barrel only.

## Feature module (copy category)

`api/{entity}.api.ts` (private `Backend*` + `toDto`, gate `data.success`) · `hooks/use{Entity}.ts` (`{entity}Keys`, staleTime 60s) · `hooks/use{Entity}Mutations.ts` (invalidate lists, optimistic delete/status) · `schemas/` (Zod, msgs from `messages.ar.ts`) · `search-params.ts` (nuqs) · `components/pages/` (exported) · `components/blocks/` (internal) · `index.ts` (sole public export)

## Actions

- **List:** nuqs `useQueryStates(parsers, { history:'replace', shallow:true })` + TanStack Query
- **Form:** `zodResolver` + `createSchema` / `updateSchema.partial()` + `FormActionsBar` + dirty `AlertDialog`
- **Fields:** EN/slug `dir="ltr"`, AR `dir="rtl"`; empty optionals → `undefined` in payload
- **New entity:** copy feature → `app/(routes)/{entities}/` (list/new/view/edit) → `config/navigation.ts` `navMain` → `lib/messages.ar.ts`
- **Layout:** `PageContainer` → `PageHeader` → `ListPageCard` or `FormActionsBar`; tables use shared `DataTable`

## Verify

`cd client/admin-panel && npm run typecheck && npm run build`

---

# Testing

**Applies to:** `backend.Tests/**/*`

**Stack:** xUnit + Testcontainers PostgreSQL + `WebApplicationFactory<Program>`. **Template:** `Features/CategoryFeature/`. Docker required.

**Handler:** `[Collection("Database")]` · `fixture.CreateContext()` · `new {Op}Handler(context)` · assert `IsSuccess` or error contains `"not found"`/`"already exists"`. No MediatR mock, no `UseInMemoryDatabase`.

**Integration:** `TestWebApplicationFactory(connectionString)` + `HttpClient` · assert status + JSON `success`/`data` · `Guid.NewGuid()` in names · `[Collection("Database")]`.

**Needs:** `public partial class Program { }` · `Npgsql.EnableLegacyTimestampBehavior` in `Program.cs` + `DatabaseFixture`.

**Skip:** scaffold mapping tests, JSON snapshots, in-memory DB.

**Frontend (no tests yet):** `npm run typecheck && npm run build` in `client/admin-panel/`.
