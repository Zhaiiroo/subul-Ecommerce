import {
  createSearchParamsCache,
  parseAsInteger,
  parseAsString,
  parseAsStringEnum,
} from 'nuqs/server';
import { ADMIN_USER_DEFAULT_PAGE_SIZE, ADMIN_USER_ROLES } from './constants';
import type { AdminUserRole } from './types';

const PAGE_SIZE_OPTIONS = [10, 20, 30, 40, 50] as const;

export const adminUserListingParsers = {
  page: parseAsInteger.withDefault(1),
  limit: parseAsInteger.withDefault(ADMIN_USER_DEFAULT_PAGE_SIZE).withOptions({
    clearOnDefault: true,
  }),
  search: parseAsString.withDefault(''),
  role: parseAsStringEnum(ADMIN_USER_ROLES as AdminUserRole[]),
  status: parseAsStringEnum(['active', 'inactive'] as const),
};

export function normalizeAdminUserPageSize(limit: number): number {
  return PAGE_SIZE_OPTIONS.includes(limit as (typeof PAGE_SIZE_OPTIONS)[number])
    ? limit
    : ADMIN_USER_DEFAULT_PAGE_SIZE;
}

export const adminUserListingSearchParamsCache =
  createSearchParamsCache(adminUserListingParsers);
