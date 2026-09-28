# Storefront — Customer App

RTL Arabic customer-facing storefront for Subul Ecommerce. Built with Next.js 16, connected to the ASP.NET Core API.

## Stack

| Layer | Tech |
|---|---|
| Framework | Next.js 16 (App Router), React 19 |
| Language | TypeScript (strict) |
| Styling | Tailwind CSS v4, shadcn/ui, RTL |
| Server state | TanStack Query v5 |
| URL state | nuqs v2 |
| HTTP client | Axios → `NEXT_PUBLIC_API_URL` |
| Forms | react-hook-form + Zod |
| Notifications | Sonner |
| Backend | ASP.NET Core 10 at `localhost:5101` |

## Getting started

```bash
cp .env.example .env.local
npm install
npm run dev -- --port 3001    # http://localhost:3001
```

## Environment variables

| Variable | Description |
|---|---|
| `NEXT_PUBLIC_API_URL` | Browser-facing ASP.NET Core API URL |
| `NEXT_PUBLIC_SITE_URL` | Canonical public storefront URL |
| `NEXT_PUBLIC_IMAGE_URL` | Optional public Cloudflare R2 custom domain used in production |
| `INTERNAL_API_ORIGIN` | Server-only API origin (`http://api:5101` inside Docker) |

Copy `.env.example` to `.env.local` for native Development.

## Routes

| Route | Description |
|---|---|
| `/` | Homepage — featured products, categories, collections |
| `/products` | Product listing with filters |
| `/products/[id]` | Product detail page |
| `/categories/[id]` | Category product listing |
| `/collections/[id]` | Collection page |
| `/cart` | Shopping cart |
| `/checkout` | Guest checkout |
| `/order-confirmation` | Post-checkout success |
| `/orders/track` | Guest order tracking |

## Feature modules

```
features/
├── product/       # Browse, detail, images
├── category/      # Read-only navigation + listing
├── cart/          # Session-based cart (X-Cart-Session)
├── checkout/      # Guest order creation
├── collection/    # Marketing collections
├── order/         # Guest order tracking
├── shipping-zone/ # Active zones for checkout
└── payment-method/# Active methods display
```

## Verify

```bash
npm run typecheck
npm run build
```
