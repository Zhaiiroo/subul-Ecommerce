// features/admin-user/constants/index.ts

import type { StatusTone } from '@/components/ui/status-indicator';
import type { AdminUserRole, AdminUserStatus } from '../types';

export const ADMIN_USER_QUERY_KEYS = {
  ALL: ['admin-users'] as const,
};

export const ADMIN_USER_DEFAULT_PAGE_SIZE = 10;
export const ADMIN_USER_MAX_PAGE_SIZE = 100;

/** Mirrors backend `AdminPasswordPolicy`. */
export const ADMIN_USER_PASSWORD_MIN_LENGTH = 10;
export const ADMIN_USER_PASSWORD_MAX_LENGTH = 128;

export const ADMIN_USER_ROLES: AdminUserRole[] = ['superadmin', 'manager', 'staff'];

export const ADMIN_USER_STATUS_TONES: Record<AdminUserStatus, StatusTone> = {
  active: 'success',
  inactive: 'neutral',
};

export const ADMIN_USER_STATUS_COLORS: Record<AdminUserStatus, string> = {
  active:
    'bg-green-50 text-green-700 border-green-200 dark:bg-green-950/40 dark:text-green-400 dark:border-green-900',
  inactive:
    'bg-gray-50 text-gray-700 border-gray-200 dark:bg-gray-900/40 dark:text-gray-400 dark:border-gray-800',
};

export const ADMIN_USER_STATUS_DOT_COLORS: Record<AdminUserStatus, string> = {
  active: 'bg-green-500',
  inactive: 'bg-muted-foreground/60',
};

export const ADMIN_USER_ROLE_COLORS: Record<AdminUserRole, string> = {
  superadmin:
    'bg-amber-50 text-amber-700 border-amber-200 dark:bg-amber-950/40 dark:text-amber-400 dark:border-amber-900',
  manager:
    'bg-blue-50 text-blue-700 border-blue-200 dark:bg-blue-950/40 dark:text-blue-400 dark:border-blue-900',
  staff:
    'bg-slate-50 text-slate-700 border-slate-200 dark:bg-slate-900/40 dark:text-slate-400 dark:border-slate-800',
};
