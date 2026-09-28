'use client';

import { useQuery } from '@tanstack/react-query';
import { getAdminUsers, getAdminUserById } from '../api/admin-user.api';
import type { AdminUserDto, AdminUserQueryParams } from '../types';
import { ADMIN_USER_QUERY_KEYS } from '../constants';

export const adminUserKeys = {
  all: ADMIN_USER_QUERY_KEYS.ALL,
  lists: () => [...adminUserKeys.all, 'list'] as const,
  list: (params: AdminUserQueryParams) => [...adminUserKeys.lists(), params] as const,
  details: () => [...adminUserKeys.all, 'detail'] as const,
  detail: (id: number) => [...adminUserKeys.details(), id] as const,
};

export function useAdminUsers(params: AdminUserQueryParams = {}, enabled = true) {
  return useQuery({
    queryKey: adminUserKeys.list(params),
    queryFn: () => getAdminUsers(params),
    placeholderData: (prev) => prev,
    staleTime: 60_000,
    enabled,
  });
}

export function useAdminUser(
  id: number,
  options?: { enabled?: boolean; initialData?: AdminUserDto },
) {
  return useQuery({
    queryKey: adminUserKeys.detail(id),
    queryFn: () => getAdminUserById(id),
    enabled: (options?.enabled ?? true) && id > 0,
    initialData: options?.initialData ?? undefined,
    staleTime: 60_000,
  });
}
