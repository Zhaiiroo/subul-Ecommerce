"use client"

import * as React from "react"
import Image from "next/image"
import Link from "next/link"
import { useKBar } from "kbar"
import { useSession } from "next-auth/react"

import { NavMain } from "@/components/nav-main"
import { NavSecondary, type NavSecondaryItem } from "@/components/nav-secondary"
import { NavUser } from "@/components/nav-user"
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
} from "@/components/ui/sidebar"
import { navSecondary, visibleNavMain } from "@/config/navigation"
import { messages } from "@/lib/messages.ar"

export function AppSidebar({ ...props }: React.ComponentProps<typeof Sidebar>) {
  const { query } = useKBar()
  const { data: session } = useSession()

  // Admin-user management is superadmin-only on the API; hide the entry rather
  // than route people to a page that answers 403.
  const mainItems = visibleNavMain(session?.user?.role).map((item) => ({
    title: item.title,
    url: item.url,
    icon: <item.icon />,
  }))

  const secondaryItems: NavSecondaryItem[] = navSecondary.map((item) => ({
    title: item.title,
    icon: <item.icon />,
    onClick: () => query.toggle(),
  }))

  return (
    <Sidebar collapsible="offcanvas" side="right" {...props}>
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton
              asChild
              className="data-[slot=sidebar-menu-button]:p-1.5!"
            >
              <Link href="/dashboard">
                <Image
                  src="/assets/logo_subul-brand_full_20260829_black.png"
                  alt=""
                  width={20}
                  height={20}
                  className="size-5! shrink-0 object-contain dark:hidden"
                />
                <Image
                  src="/assets/logo_subul-brand_full_20260829_white.png"
                  alt=""
                  width={20}
                  height={20}
                  className="hidden size-5! shrink-0 object-contain dark:block"
                />
                <span className="text-base font-semibold">
                  {messages.common.companyName}
                </span>
              </Link>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>
      <SidebarContent>
        <NavMain items={mainItems} />
      </SidebarContent>
      <SidebarFooter>
        <NavSecondary items={secondaryItems} />
        <NavUser />
      </SidebarFooter>
    </Sidebar>
  )
}
