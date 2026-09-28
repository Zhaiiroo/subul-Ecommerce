// features/admin-user/index.ts
// Public barrel — the ONLY way to import from the admin-user feature.

export { AdminUserListingPage } from './components/pages/admin-user-listing-page';
export { AdminUserForm } from './components/pages/admin-user-form';
export { AdminUserView } from './components/pages/admin-user-view';

export { useAdminUsers, useAdminUser, adminUserKeys } from './hooks/useAdminUser';
export {
  useCreateAdminUser,
  useUpdateAdminUser,
  useDeleteAdminUser,
  useChangeAdminUserStatus,
  useResetAdminUserPassword,
} from './hooks/useAdminUserMutations';

export type {
  AdminUserDto,
  AdminUserListItem,
  AdminUserQueryParams,
  AdminUserRole,
  AdminUserStatus,
  TemporaryCredential,
} from './types';

export {
  createAdminUserSchema,
  updateAdminUserSchema,
} from './schemas/admin-user.schema';
export type {
  CreateAdminUserInput,
  UpdateAdminUserInput,
} from './schemas/admin-user.schema';

export { getAdminUsers, getAdminUserById } from './api/admin-user.api';
export { getCachedAdminUserById } from './api/admin-user.cached';

export {
  adminUserListingParsers,
  adminUserListingSearchParamsCache,
} from './search-params';
