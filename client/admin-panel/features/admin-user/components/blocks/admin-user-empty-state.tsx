'use client';

import Link from 'next/link';
import { Plus, UsersRound } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { messages } from '@/lib/messages.ar';

const m = messages.adminUser.listing;

export function AdminUserEmptyState() {
  return (
    <div className="flex flex-col items-center justify-center gap-3 px-4 py-16 text-center">
      <div className="bg-muted flex size-14 items-center justify-center rounded-full">
        <UsersRound className="text-muted-foreground size-7" />
      </div>
      <div className="space-y-1">
        <p className="font-medium">{m.emptyTitle}</p>
        <p className="text-muted-foreground text-sm">{m.emptyDescription}</p>
      </div>
      <Button asChild className="mt-2">
        <Link href="/admin-users/new">
          <Plus className="size-4" />
          {m.emptyAction}
        </Link>
      </Button>
    </div>
  );
}
