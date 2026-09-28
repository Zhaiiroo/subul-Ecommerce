'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { messages } from '@/lib/messages.ar';
import {
  createAdminUser,
  updateAdminUser,
  deleteAdminUser,
  changeAdminUserStatus,
  resetAdminUserPassword,
  type CreateAdminUserPayload,
  type UpdateAdminUserPayload,
} from '../api/admin-user.api';
import { adminUserKeys } from './useAdminUser';
import type { AdminUserDto, AdminUserListItem } from '../types';
import type { PaginatedResponse } from '@/types/api';

export function useCreateAdminUser() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: CreateAdminUserPayload) => createAdminUser(payload),
    onSuccess: () => {
      // Lists only: the create response is a partial record, and seeding the
      // detail cache with it would shadow the full one on the view screen.
      queryClient.invalidateQueries({ queryKey: adminUserKeys.lists() });
      toast.success(messages.adminUser.createSuccess);
    },
    onError: (error: Error) => {
      toast.error(error.message ?? messages.adminUser.createError);
    },
  });
}

export function useUpdateAdminUser() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, payload }: { id: number; payload: UpdateAdminUserPayload }) =>
      updateAdminUser(id, payload),
    onSuccess: (updated) => {
      queryClient.invalidateQueries({ queryKey: adminUserKeys.lists() });
      queryClient.setQueryData(adminUserKeys.detail(updated.id), updated);
      toast.success(messages.adminUser.updateSuccess);
    },
    onError: (error: Error) => {
      toast.error(error.message ?? messages.adminUser.updateError);
    },
  });
}

export function useDeleteAdminUser() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: number) => deleteAdminUser(id),
    onMutate: async (id) => {
      await queryClient.cancelQueries({ queryKey: adminUserKeys.lists() });

      const previousLists = queryClient.getQueriesData<PaginatedResponse<AdminUserListItem>>({
        queryKey: adminUserKeys.lists(),
      });

      previousLists.forEach(([key, data]) => {
        if (!data) return;
        queryClient.setQueryData(key, {
          ...data,
          items: data.items.filter((item) => item.id !== id),
          total: Math.max(0, data.total - 1),
        });
      });

      return { previousLists, id };
    },
    onError: (error: Error, _id, context) => {
      context?.previousLists.forEach(([key, data]) => {
        queryClient.setQueryData(key, data);
      });
      // The backend refuses to delete an account that is linked to existing
      // records and says why, so its message beats a generic one here.
      toast.error(error.message ?? messages.adminUser.deleteError);
    },
    onSuccess: (_data, id) => {
      queryClient.invalidateQueries({ queryKey: adminUserKeys.lists() });
      queryClient.removeQueries({ queryKey: adminUserKeys.detail(id) });
      toast.success(messages.adminUser.deleteSuccess);
    },
  });
}

export function useChangeAdminUserStatus() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, isActive }: { id: number; isActive: boolean }) =>
      changeAdminUserStatus(id, { isActive }),
    onMutate: async ({ id, isActive }) => {
      await queryClient.cancelQueries({ queryKey: adminUserKeys.lists() });
      await queryClient.cancelQueries({ queryKey: adminUserKeys.detail(id) });

      const nextStatus = isActive ? 'active' : 'inactive';

      const previousLists = queryClient.getQueriesData<PaginatedResponse<AdminUserListItem>>({
        queryKey: adminUserKeys.lists(),
      });

      const previousDetail = queryClient.getQueryData<AdminUserDto>(adminUserKeys.detail(id));

      previousLists.forEach(([key, data]) => {
        if (!data) return;
        queryClient.setQueryData(key, {
          ...data,
          items: data.items.map((item) =>
            item.id === id ? { ...item, status: nextStatus } : item,
          ),
        });
      });

      if (previousDetail) {
        queryClient.setQueryData(adminUserKeys.detail(id), {
          ...previousDetail,
          status: nextStatus,
        });
      }

      return { previousLists, previousDetail, id };
    },
    onError: (error: Error, _vars, context) => {
      context?.previousLists.forEach(([key, data]) => {
        queryClient.setQueryData(key, data);
      });
      if (context?.previousDetail) {
        queryClient.setQueryData(adminUserKeys.detail(context.id), context.previousDetail);
      }
      toast.error(error.message ?? messages.adminUser.status.changeError);
    },
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: adminUserKeys.lists() });
      queryClient.setQueryData(
        adminUserKeys.detail(result.id),
        (current: AdminUserDto | undefined) =>
          current
            ? {
                ...current,
                status: result.status,
                updatedAt: result.updatedAt ?? current.updatedAt,
              }
            : current,
      );
      toast.success(messages.adminUser.status.changeSuccess);
    },
  });
}

export function useResetAdminUserPassword() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, name }: { id: number; name: string }) =>
      resetAdminUserPassword(id, name),
    onSuccess: (credential) => {
      // The account now owes a password change, so both caches are stale.
      queryClient.invalidateQueries({ queryKey: adminUserKeys.lists() });
      queryClient.invalidateQueries({
        queryKey: adminUserKeys.detail(credential.adminUserId),
      });
      toast.success(messages.adminUser.reset.success);
    },
    onError: (error: Error) => {
      toast.error(error.message ?? messages.adminUser.reset.error);
    },
  });
}
