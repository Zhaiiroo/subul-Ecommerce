import { Badge } from '@/components/ui/badge';
import { messages } from '@/lib/messages.ar';
import { cn } from '@/lib/utils';
import { ADMIN_USER_ROLE_COLORS } from '../../constants';
import type { AdminUserRole } from '../../types';

const roleLabels = messages.adminUser.role;

interface AdminUserRoleBadgeProps {
  role: AdminUserRole;
  className?: string;
}

export function AdminUserRoleBadge({ role, className }: AdminUserRoleBadgeProps) {
  return (
    <Badge variant="outline" className={cn(ADMIN_USER_ROLE_COLORS[role], className)}>
      {roleLabels[role] ?? role}
    </Badge>
  );
}
