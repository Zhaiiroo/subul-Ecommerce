// features/admin-user/types/index.ts

export type AdminUserStatus = 'active' | 'inactive';

export type AdminUserRole = 'superadmin' | 'manager' | 'staff';

export interface AdminUserDto {
  id: number;
  name: string;
  email: string;
  role: AdminUserRole;
  status: AdminUserStatus;
  mustChangePassword: boolean;
  lastLoginAt: string | null;
  passwordChangedAt: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface AdminUserListItem {
  id: number;
  name: string;
  email: string;
  role: AdminUserRole;
  status: AdminUserStatus;
  mustChangePassword: boolean;
  lastLoginAt: string | null;
}

export interface AdminUserQueryParams {
  page?: number;
  limit?: number;
  search?: string;
  role?: AdminUserRole;
  status?: AdminUserStatus;
  sortBy?: 'name' | 'email' | 'createdAt' | 'lastLoginAt';
  sortOrder?: 'asc' | 'desc';
}

/**
 * A plaintext password the API returns exactly once — on create when it
 * generated one, and on every reset. It is never stored client-side.
 */
export interface TemporaryCredential {
  adminUserId: number;
  name: string;
  email: string;
  temporaryPassword: string;
}
