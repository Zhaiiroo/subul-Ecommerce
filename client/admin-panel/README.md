# Admin Panel — Template

Next.js 16 admin panel template with RTL Arabic support, shadcn/ui, and a wired ASP.NET Core backend. The `features/category/` folder is the canonical pattern — clone it when adding a new feature.

---

## Stack

| Layer | Tech |
|---|---|
| Framework | Next.js 16 (App Router), React 19 |
| Language | TypeScript (strict) |
| Styling | Tailwind CSS v4, shadcn/ui (radix-nova), RTL |
| Server state | TanStack Query v5 |
| URL state | nuqs v2 |
| HTTP client | Axios → `NEXT_PUBLIC_API_URL` |
| Forms | react-hook-form + Zod |
| Notifications | Sonner |
| Command palette | kbar |
| Theming | next-themes (`d` key toggles dark/light) |
| Backend | ASP.NET Core 10 at `localhost:5101` |

---

## Getting started

```bash
cp .env.example .env.local   # fill in NEXT_PUBLIC_API_URL
npm install
npm run dev                  # http://localhost:3000 → redirects to /dashboard
```

---

## Project structure

```
admin-panel/
├── app/
│   ├── layout.tsx              # Root layout — fonts, metadata, providers
│   ├── page.tsx                # Redirects to /dashboard
│   ├── error.tsx               # Global error boundary
│   ├── not-found.tsx           # Custom 404 page
│   └── (routes)/
│       ├── layout.tsx          # Sidebar + header layout for all admin routes
│       ├── loading.tsx         # Route-level skeleton loader
│       ├── dashboard/          # Demo dashboard (charts, KPI cards)
│       └── categories/         # Category CRUD (real API)
│
├── components/
│   ├── app-providers.tsx       # Wraps QueryClient, Theme, RTL, KBar, Toaster
│   ├── app-sidebar.tsx         # Sidebar — reads nav from config/navigation.ts
│   ├── site-header.tsx         # Top header with sidebar trigger
│   ├── kbar/                   # Command palette UI
│   ├── layout/page-container.tsx
│   └── ui/                     # shadcn/ui primitives (do not edit manually)
│
├── config/
│   └── navigation.ts           # SINGLE source of truth for all nav items + KBar
│
├── features/
│   └── category/               # ← TEMPLATE for new features (see below)
│       ├── api/                # Axios calls
│       ├── hooks/              # useQuery + useMutation hooks
│       ├── components/
│       │   ├── pages/          # Route-level screens (exported publicly)
│       │   └── blocks/         # Internal UI blocks (not exported)
│       ├── schemas/            # Zod validation schemas
│       ├── search-params.ts    # nuqs parsers + server cache (listing URL state)
│       ├── types/              # TypeScript interfaces
│       └── constants/          # Shared constants (e.g. query keys)
│
├── lib/
│   ├── api-client.ts           # Axios instance (reads NEXT_PUBLIC_API_URL)
│   ├── messages.ar.ts          # All Arabic UI strings + i18n helpers
│   └── utils.ts                # cn() helper
│
├── providers/
│   └── query-client-provider.tsx
│
├── hooks/
│   └── use-mobile.ts           # useIsMobile() — 768px breakpoint
│
└── types/
    └── api.ts                  # ApiResponse<T>, PaginatedResponse<T>
```

---

## Adding a new feature

1. Copy `features/category/` → `features/{entity}/`
2. Replace every occurrence of `category`/`Category` with your entity name
3. Update `features/{entity}/api/{entity}.api.ts` with the correct endpoints
4. Update `features/{entity}/types/index.ts` with your entity shape
5. Update `features/{entity}/schemas/{entity}.schema.ts` with Zod validation
6. Update `features/{entity}/search-params.ts` with listing URL parsers (page, search, filters)
7. Add route pages under `app/(routes)/{entities}/`
8. Add the new route to `config/navigation.ts` in `navMain` — it automatically appears in the sidebar and KBar
9. Put route-level screens in `components/pages/` and reusable UI in `components/blocks/` — only export `pages/` from `index.ts`

---

## URL state (nuqs)

List pages sync filters and pagination to the URL via [nuqs](https://nuqs.dev).

**Setup (already done):**

- `NuqsAdapter` wraps the app in `app/layout.tsx`
- Each feature defines parsers in `features/{entity}/search-params.ts`

**Category example** (`features/category/search-params.ts`):

| Query key | Parser | Default | Notes |
|---|---|---|---|
| `page` | `parseAsInteger` | `1` | Pagination |
| `search` | `parseAsString` | `""` | Throttled 300ms in the listing component |
| `view` | `parseAsStringEnum(['table', 'tree'])` | `table` | Default omitted from URL (`?view=tree` only for tree) |

**Client usage** (in listing components):

```tsx
import { useQueryStates } from 'nuqs'
import { categoryListingParsers } from '../search-params'

const [{ page, search, view }, setParams] = useQueryStates(categoryListingParsers, {
  history: 'replace',
  shallow: true,
})
```

**Server usage (optional):** export `categoryListingSearchParamsCache` from the feature barrel and call `.parse(searchParams)` in a Server Component when you need URL-aware RSC prefetch.

When adding a new list feature, copy `search-params.ts` and adjust keys for entity-specific filters (`status`, `parentId`, etc.).

---

## Navigation

`config/navigation.ts` is the single source of truth:

- **`navMain`** — main sidebar links; items with a real `url` (not `"#"`) are auto-added to KBar
- **`navSecondary`** — secondary links at the bottom of the sidebar
- **`navDocuments`** — document-type links mid-sidebar
- **`kbarNavItems`** — derived automatically from `navMain` (no manual sync needed)

To add a new route to both sidebar and KBar: add one entry to `navMain`.

---

## Scripts

```bash
npm run dev        # Development server
npm run build      # Production build
npm run start      # Production server
npm run lint       # ESLint
npm run typecheck  # tsc --noEmit
npm run format     # Prettier (all .ts/.tsx)
```

---

## Adding shadcn/ui components

```bash
npx shadcn@latest add <component-name>
```

Components are placed in `components/ui/`.

---

## Environment variables

| Variable | Description |
|---|---|
| `NEXT_PUBLIC_API_URL` | Base URL for the ASP.NET Core API (e.g. `http://localhost:5101/api`) |
| `NEXT_PUBLIC_IMAGE_URL` | Optional public Cloudflare R2 custom domain used by production image optimization |
| `INTERNAL_API_URL` | Server-only API address (`http://api:5101/api` inside Docker) |
| `AUTH_SECRET` | Server-only Auth.js signing secret |

Copy `.env.example` to `.env.local` to get started.
