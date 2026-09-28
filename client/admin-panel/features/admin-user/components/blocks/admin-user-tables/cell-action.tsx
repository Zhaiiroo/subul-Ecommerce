'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useSession } from 'next-auth/react';
import { Eye, KeyRound, Pencil, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import { messages } from '@/lib/messages.ar';
import type { AdminUserListItem, TemporaryCredential } from '../../../types';
import { useDeleteAdminUser } from '../../../hooks/useAdminUserMutations';
import { AdminUserResetPasswordDialog } from '../admin-user-reset-password-dialog';

interface AdminUserCellActionProps {
  adminUser: AdminUserListItem;
  onTemporaryPassword: (credential: TemporaryCredential) => void;
}

export function AdminUserCellAction({
  adminUser,
  onTemporaryPassword,
}: AdminUserCellActionProps) {
  const { data: session } = useSession();
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [resetOpen, setResetOpen] = useState(false);
  const { mutate: deleteAdminUser, isPending: isDeleting } = useDeleteAdminUser();

  const isSelf = session?.user?.id === adminUser.id;

  function handleConfirmDelete() {
    deleteAdminUser(adminUser.id, {
      onSettled: () => setDeleteOpen(false),
    });
  }

  return (
    <>
      <div className="flex items-center justify-start gap-1">
        <Tooltip>
          <TooltipTrigger asChild>
            <span className="inline-flex">
              <Button
                asChild
                variant="outline"
                size="icon-sm"
                disabled={isDeleting}
                id={`admin-user-view-${adminUser.id}`}
                aria-label={messages.common.view}
              >
                <Link href={`/admin-users/${adminUser.id}/view`}>
                  <Eye />
                  <span className="sr-only">{messages.common.view}</span>
                </Link>
              </Button>
            </span>
          </TooltipTrigger>
          <TooltipContent side="top">{messages.common.view}</TooltipContent>
        </Tooltip>

        <Tooltip>
          <TooltipTrigger asChild>
            <span className="inline-flex">
              <Button
                asChild
                variant="outline"
                size="icon-sm"
                disabled={isDeleting}
                id={`admin-user-edit-${adminUser.id}`}
                aria-label={messages.common.edit}
              >
                <Link href={`/admin-users/${adminUser.id}/edit`}>
                  <Pencil />
                  <span className="sr-only">{messages.common.edit}</span>
                </Link>
              </Button>
            </span>
          </TooltipTrigger>
          <TooltipContent side="top">{messages.common.edit}</TooltipContent>
        </Tooltip>

        <Tooltip>
          <TooltipTrigger asChild>
            <span className="inline-flex">
              <Button
                variant="outline"
                size="icon-sm"
                onClick={() => setResetOpen(true)}
                disabled={isDeleting}
                id={`admin-user-reset-${adminUser.id}`}
                aria-label={messages.adminUser.reset.action}
              >
                <KeyRound />
                <span className="sr-only">{messages.adminUser.reset.action}</span>
              </Button>
            </span>
          </TooltipTrigger>
          <TooltipContent side="top">{messages.adminUser.reset.action}</TooltipContent>
        </Tooltip>

        {/* Deleting your own account is refused by the API; don't offer it. */}
        {!isSelf && (
          <Tooltip>
            <TooltipTrigger asChild>
              <span className="inline-flex">
                <Button
                  variant="outline"
                  size="icon-sm"
                  className="text-destructive hover:bg-destructive/10 hover:text-destructive"
                  onClick={() => setDeleteOpen(true)}
                  disabled={isDeleting}
                  id={`admin-user-delete-${adminUser.id}`}
                  aria-label={messages.common.delete}
                >
                  <Trash2 />
                  <span className="sr-only">{messages.common.delete}</span>
                </Button>
              </span>
            </TooltipTrigger>
            <TooltipContent side="top">{messages.common.delete}</TooltipContent>
          </Tooltip>
        )}
      </div>

      <AdminUserResetPasswordDialog
        open={resetOpen}
        onOpenChange={setResetOpen}
        adminUserId={adminUser.id}
        adminUserName={adminUser.name}
        onIssued={onTemporaryPassword}
      />

      <AlertDialog open={deleteOpen} onOpenChange={setDeleteOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{messages.adminUser.view.deleteTitle}</AlertDialogTitle>
            <AlertDialogDescription>
              {messages.adminUser.view.deleteDescription(adminUser.name)}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={isDeleting}>
              {messages.adminUser.form.cancel}
            </AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              onClick={handleConfirmDelete}
              disabled={isDeleting}
            >
              {messages.common.delete}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
