import { Badge } from '@/components/ui/badge';
import { messages } from '@/lib/messages.ar';
import { cn } from '@/lib/utils';
import {
  ADMIN_USER_STATUS_COLORS,
  ADMIN_USER_STATUS_DOT_COLORS,
} from '../../constants';
import type { AdminUserStatus } from '../../types';

const statusLabels = messages.adminUser.status;

interface AdminUserStatusBadgeProps {
  status: AdminUserStatus;
  className?: string;
}

export function AdminUserStatusBadge({ status, className }: AdminUserStatusBadgeProps) {
  const label = statusLabels[status] ?? status;

  return (
    <Badge variant="outline" className={cn(ADMIN_USER_STATUS_COLORS[status], className)}>
      <span
        className="relative flex size-1.5 shrink-0"
        data-icon="inline-start"
        aria-hidden="true"
      >
        {status === 'active' && (
          <span className="absolute inline-flex size-full animate-ping rounded-full bg-green-400 opacity-60 motion-reduce:animate-none dark:bg-green-500 dark:opacity-50" />
        )}
        <span
          className={cn(
            'relative size-1.5 rounded-full',
            ADMIN_USER_STATUS_DOT_COLORS[status],
            status === 'active' && 'motion-reduce:animate-none animate-pulse',
          )}
        />
      </span>
      {label}
    </Badge>
  );
}
