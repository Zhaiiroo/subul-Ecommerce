import apiClient from '@/lib/api-client';
import type {
  AdminUserDto,
  AdminUserListItem,
  AdminUserQueryParams,
  AdminUserRole,
  TemporaryCredential,
} from '../types';
import type { ApiResponse, PaginatedResponse } from '@/types/api';

interface BackendAdminUserItem {
  id: number;
  name: string;
  email: string;
  role: string;
  isActive: boolean;
  mustChangePassword: boolean;
  lastLoginAt: string | null;
  passwordChangedAt: string | null;
  createdAt: string;
  updatedAt: string | null;
}

interface BackendCreatedAdminUser {
  id: number;
  name: string;
  email: string;
  role: string;
  isActive: boolean;
  mustChangePassword: boolean;
  createdAt: string;
  updatedAt: string | null;
  temporaryPassword: string | null;
}

interface BackendResetPassword {
  id: number;
  email: string;
  mustChangePassword: boolean;
  passwordChangedAt: string | null;
  temporaryPassword: string | null;
}

function normalizeRole(role: string): AdminUserRole {
  return role === 'superadmin' || role === 'manager' ? role : 'staff';
}

function toListItem(raw: BackendAdminUserItem): AdminUserListItem {
  return {
    id: raw.id,
    name: raw.name,
    email: raw.email,
    role: normalizeRole(raw.role),
    status: raw.isActive ? 'active' : 'inactive',
    mustChangePassword: raw.mustChangePassword,
    lastLoginAt: raw.lastLoginAt,
  };
}

function toDto(raw: BackendAdminUserItem): AdminUserDto {
  return {
    id: raw.id,
    name: raw.name,
    email: raw.email,
    role: normalizeRole(raw.role),
    status: raw.isActive ? 'active' : 'inactive',
    mustChangePassword: raw.mustChangePassword,
    lastLoginAt: raw.lastLoginAt,
    passwordChangedAt: raw.passwordChangedAt,
    createdAt: raw.createdAt,
    updatedAt: raw.updatedAt,
  };
}

export interface CreateAdminUserPayload {
  name: string;
  email: string;
  role: AdminUserRole;
  /** Omitted, the API generates a temporary password and returns it once. */
  password?: string;
}

export interface UpdateAdminUserPayload {
  name: string;
  email: string;
  role: AdminUserRole;
}

export interface ChangeAdminUserStatusPayload {
  isActive: boolean;
}

export interface ChangeAdminUserStatusResult {
  id: number;
  status: 'active' | 'inactive';
  updatedAt: string | null;
}

/**
 * Narrower than {@link AdminUserDto} on purpose: the create response carries no
 * login or password-change timestamps, and inventing nulls for them would put a
 * wrong record into the detail cache.
 */
export interface CreatedAdminUser {
  id: number;
  name: string;
  email: string;
  role: AdminUserRole;
  status: 'active' | 'inactive';
  mustChangePassword: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface CreateAdminUserResult {
  adminUser: CreatedAdminUser;
  /** Present only when the API generated the password. Show once, never persist. */
  temporaryPassword: string | null;
}

export async function createAdminUser(
  payload: CreateAdminUserPayload,
): Promise<CreateAdminUserResult> {
  const { data } = await apiClient.post<ApiResponse<BackendCreatedAdminUser>>('/admin-users', {
    name: payload.name,
    email: payload.email,
    role: payload.role,
    password: payload.password ?? null,
  });

  if (!data.success) throw new Error(data.message ?? 'Failed to create admin user');

  const raw = data.data!;
  return {
    adminUser: {
      id: raw.id,
      name: raw.name,
      email: raw.email,
      role: normalizeRole(raw.role),
      status: raw.isActive ? 'active' : 'inactive',
      mustChangePassword: raw.mustChangePassword,
      createdAt: raw.createdAt,
      updatedAt: raw.updatedAt,
    },
    temporaryPassword: raw.temporaryPassword,
  };
}

export async function updateAdminUser(
  id: number,
  payload: UpdateAdminUserPayload,
): Promise<AdminUserDto> {
  const { data } = await apiClient.put<ApiResponse<BackendAdminUserItem>>(`/admin-users/${id}`, {
    name: payload.name,
    email: payload.email,
    role: payload.role,
  });

  if (!data.success) throw new Error(data.message ?? 'Failed to update admin user');
  return toDto(data.data!);
}

export async function deleteAdminUser(id: number): Promise<void> {
  const { data } = await apiClient.delete<ApiResponse<boolean>>(`/admin-users/${id}`);
  if (!data.success) throw new Error(data.message ?? 'Failed to delete admin user');
}

export async function changeAdminUserStatus(
  id: number,
  payload: ChangeAdminUserStatusPayload,
): Promise<ChangeAdminUserStatusResult> {
  const { data } = await apiClient.put<
    ApiResponse<{
      id: number;
      isActive: boolean;
      updatedAt: string | null;
    }>
  >(`/admin-users/${id}/status`, {
    isActive: payload.isActive,
  });

  if (!data.success) throw new Error(data.message ?? 'Failed to change admin user status');

  const raw = data.data!;
  return {
    id: raw.id,
    status: raw.isActive ? 'active' : 'inactive',
    updatedAt: raw.updatedAt,
  };
}

export async function resetAdminUserPassword(
  id: number,
  name: string,
): Promise<TemporaryCredential> {
  const { data } = await apiClient.post<ApiResponse<BackendResetPassword>>(
    `/admin-users/${id}/reset-password`,
    { newPassword: null },
  );

  if (!data.success) throw new Error(data.message ?? 'Failed to reset password');

  const raw = data.data!;
  return {
    adminUserId: raw.id,
    name,
    email: raw.email,
    temporaryPassword: raw.temporaryPassword ?? '',
  };
}

export async function getAdminUserById(id: number): Promise<AdminUserDto | null> {
  const { data } = await apiClient.get<ApiResponse<BackendAdminUserItem>>(`/admin-users/${id}`);
  if (!data.success) return null;
  return data.data ? toDto(data.data) : null;
}

export async function getAdminUsers(
  params: AdminUserQueryParams = {},
): Promise<PaginatedResponse<AdminUserListItem>> {
  const { page = 1, limit = 10, search, role, status, sortBy, sortOrder } = params;

  const { data } = await apiClient.get<
    ApiResponse<{
      items: BackendAdminUserItem[];
      total: number;
      page: number;
      limit: number;
      totalPages: number;
    }>
  >('/admin-users', {
    params: {
      page,
      limit,
      ...(search && { search }),
      ...(role && { role }),
      ...(status !== undefined && { isActive: status === 'active' }),
      ...(sortBy && { sortBy }),
      ...(sortOrder && { sortOrder }),
    },
  });

  if (!data.success) throw new Error(data.message ?? 'Failed to fetch admin users');

  const raw = data.data!;
  return {
    items: raw.items.map(toListItem),
    total: raw.total,
    page: raw.page,
    limit: raw.limit,
    totalPages: raw.totalPages,
  };
}
