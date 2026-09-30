import { AnnouncementBar } from "@/components/storefront/announcement-bar"
import { StorefrontFooter } from "@/components/storefront/footer"
import { StorefrontHeader } from "@/components/storefront/header"
import { getCachedTopLevelCategories, type CategoryListItem } from "@/features/category"

// Every page under this layout renders per request. The nav comes from the API,
// and a page prerendered at build time would freeze it — worse, a Docker build
// has no API to reach, so those pages shipped with an empty nav. Rendering per
// request also gives the API calls a visitor to attribute them to (see
// lib/api-client.ts).
export const dynamic = "force-dynamic"

export default async function StoreLayout({
  children,
}: {
  children: React.ReactNode
}) {
  // Fetched here rather than in the header itself: the nav then ships inside the
  // first HTML response instead of popping in — and every page under this layout
  // shares the one request.
  let categories: CategoryListItem[] = []
  try {
    categories = await getCachedTopLevelCategories()
  } catch {
    // API unavailable — render the header without category links.
  }

  return (
    <div className="flex min-h-screen flex-col">
      <AnnouncementBar />
      <StorefrontHeader categories={categories} />
      <main className="flex flex-1 flex-col">{children}</main>
      <StorefrontFooter />
    </div>
  )
}
