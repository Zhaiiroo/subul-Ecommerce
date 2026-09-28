'use client';

import { Loader2 } from 'lucide-react';
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import { messages } from '@/lib/messages.ar';
import { cn } from '@/lib/utils';
import type { AdminUserStatus } from '../../types';
import { useChangeAdminUserStatus } from '../../hooks/useAdminUserMutations';
import { AdminUserStatusBadge } from './admin-user-status-badge';

interface AdminUserStatusToggleProps {
  adminUserId: number;
  status: AdminUserStatus;
  /**
   * The signed-in admin cannot deactivate themselves — the API refuses it, so
   * the control is inert rather than offering an action that will fail.
   */
  disabled?: boolean;
  className?: string;
}

export function AdminUserStatusToggle({
  adminUserId,
  status,
  disabled = false,
  className,
}: AdminUserStatusToggleProps) {
  const { mutate, isPending } = useChangeAdminUserStatus();

  const nextIsActive = status !== 'active';
  const tooltip = nextIsActive
    ? messages.adminUser.status.toggleActive
    : messages.adminUser.status.toggleInactive;

  function handleToggle() {
    mutate({ id: adminUserId, isActive: nextIsActive });
  }

  if (disabled) {
    return <AdminUserStatusBadge status={status} className={className} />;
  }

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <button
          type="button"
          onClick={handleToggle}
          disabled={isPending}
          aria-label={tooltip}
          className={cn(
            'inline-flex items-center rounded-full transition-opacity focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none disabled:cursor-wait disabled:opacity-60',
            className,
          )}
        >
          <AdminUserStatusBadge status={status} />
          {isPending && (
            <Loader2 className="text-muted-foreground ms-1.5 size-3.5 animate-spin" />
          )}
        </button>
      </TooltipTrigger>
      <TooltipContent side="top">{tooltip}</TooltipContent>
    </Tooltip>
  );
}
