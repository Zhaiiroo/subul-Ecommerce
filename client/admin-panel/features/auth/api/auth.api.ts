import apiClient from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';

export interface ChangePasswordPayload {
  currentPassword: string;
  newPassword: string;
}

/**
 * Stays reachable while the account owes a password change — the backend keeps
 * `api/auth/change-password` open and closes everything else.
 */
export async function changeMyPassword(payload: ChangePasswordPayload): Promise<void> {
  const { data } = await apiClient.post<ApiResponse<{ success: boolean }>>(
    '/auth/change-password',
    payload,
  );
  if (!data.success) throw new Error(data.message ?? 'Failed to change password');
}
