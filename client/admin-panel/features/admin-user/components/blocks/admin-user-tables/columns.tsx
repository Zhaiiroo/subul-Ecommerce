'use client';

import type { ColumnDef } from '@tanstack/react-table';
import { useSession } from 'next-auth/react';
import { KeyRound } from 'lucide-react';
import { formatDate, messages } from '@/lib/messages.ar';
import type { AdminUserListItem, TemporaryCredential } from '../../../types';
import { AdminUserCellAction } from './cell-action';
import { AdminUserRoleBadge } from '../admin-user-role-badge';
import { AdminUserStatusToggle } from '../admin-user-status-toggle';

const m = messages.adminUser.listing;

function StatusCell({ adminUser }: { adminUser: AdminUserListItem }) {
  const { data: session } = useSession();

  return (
    <AdminUserStatusToggle
      adminUserId={adminUser.id}
      status={adminUser.status}
      disabled={session?.user?.id === adminUser.id}
    />
  );
}

export function createAdminUserColumns(
  onTemporaryPassword: (credential: TemporaryCredential) => void,
): ColumnDef<AdminUserListItem>[] {
  return [
    {
      accessorKey: 'name',
      header: m.columnName,
      cell: ({ row }) => (
        <div className="space-y-0.5">
          <p className="font-medium">{row.original.name}</p>
          <code
            className="bg-muted text-muted-foreground rounded px-1.5 py-0.5 text-xs"
            dir="ltr"
          >
            {row.original.email}
          </code>
        </div>
      ),
    },
    {
      accessorKey: 'role',
      header: m.columnRole,
      cell: ({ row }) => <AdminUserRoleBadge role={row.original.role} />,
    },
    {
      accessorKey: 'status',
      header: m.columnStatus,
      cell: ({ row }) => (
        <div className="flex flex-wrap items-center gap-1.5">
          <StatusCell adminUser={row.original} />
          {row.original.mustChangePassword && (
            <span className="text-muted-foreground inline-flex items-center gap-1 text-xs">
              <KeyRound className="size-3" aria-hidden />
              {messages.adminUser.pendingPasswordChange}
            </span>
          )}
        </div>
      ),
    },
    {
      accessorKey: 'lastLoginAt',
      header: m.columnLastLogin,
      cell: ({ row }) =>
        row.original.lastLoginAt ? (
          <span className="text-sm">
            {formatDate(row.original.lastLoginAt, {
              dateStyle: 'medium',
              timeStyle: 'short',
            })}
          </span>
        ) : (
          <span className="text-muted-foreground text-sm">{m.neverLoggedIn}</span>
        ),
    },
    {
      id: 'actions',
      header: m.columnActions,
      cell: ({ row }) => (
        <AdminUserCellAction
          adminUser={row.original}
          onTemporaryPassword={onTemporaryPassword}
        />
      ),
    },
  ];
}
