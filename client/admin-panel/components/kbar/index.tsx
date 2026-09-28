"use client"

import {
  KBarAnimator,
  KBarPortal,
  KBarPositioner,
  KBarProvider,
  KBarSearch,
} from "kbar"
import { useRouter } from "next/navigation"
import { useSession } from "next-auth/react"
import { useMemo } from "react"

import RenderResults from "@/components/kbar/render-result"
import { canSeeNavItem, kbarNavItems, kbarQuickActions } from "@/config/navigation"
import { messages } from "@/lib/messages.ar"

export function KBar({ children }: { children: React.ReactNode }) {
  const router = useRouter()
  const { data: session } = useSession()
  const role = session?.user?.role

  const actions = useMemo(
    () =>
      [...kbarNavItems, ...kbarQuickActions]
        // Same role filter the sidebar applies, so the palette cannot route
        // someone to a screen the API will answer 403 for.
        .filter((item) => canSeeNavItem(item, role))
        .map((item) => ({
          id: item.id,
          name: item.name,
          section: item.section,
          subtitle: item.subtitle,
          keywords: `${item.name} ${item.subtitle}`,
          perform: () => router.push(item.url),
        })),
    [router, role]
  )

  return (
    <KBarProvider actions={actions}>
      <KBarPortal>
        <KBarPositioner className="fixed inset-0 z-99999 bg-background/80 p-0 backdrop-blur-sm">
          <KBarAnimator className="relative mt-64 w-full max-w-[600px] -translate-y-12 overflow-hidden rounded-lg border bg-card text-card-foreground shadow-lg">
            <div className="sticky top-0 z-10 border-b border-border bg-card">
              <KBarSearch
                defaultPlaceholder={messages.kbar.searchPlaceholder}
                className="w-full border-none bg-card px-6 py-4 text-lg outline-hidden focus:ring-0 focus:ring-offset-0 focus:outline-hidden"
              />
            </div>
            <div className="max-h-[400px]">
              <RenderResults />
            </div>
          </KBarAnimator>
        </KBarPositioner>
      </KBarPortal>
      {children}
    </KBarProvider>
  )
}
