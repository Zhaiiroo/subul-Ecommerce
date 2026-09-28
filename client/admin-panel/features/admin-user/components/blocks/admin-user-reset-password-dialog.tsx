'use client';

import { Loader2 } from 'lucide-react';
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
import { messages } from '@/lib/messages.ar';
import { useResetAdminUserPassword } from '../../hooks/useAdminUserMutations';
import type { TemporaryCredential } from '../../types';

const m = messages.adminUser.reset;

interface AdminUserResetPasswordDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  adminUserId: number;
  adminUserName: string;
  onIssued: (credential: TemporaryCredential) => void;
}

export function AdminUserResetPasswordDialog({
  open,
  onOpenChange,
  adminUserId,
  adminUserName,
  onIssued,
}: AdminUserResetPasswordDialogProps) {
  const { mutate: resetPassword, isPending } = useResetAdminUserPassword();

  function handleConfirm() {
    resetPassword(
      { id: adminUserId, name: adminUserName },
      {
        onSuccess: (credential) => onIssued(credential),
        onSettled: () => onOpenChange(false),
      },
    );
  }

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{m.title}</AlertDialogTitle>
          <AlertDialogDescription>{m.description(adminUserName)}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={isPending}>
            {messages.adminUser.form.cancel}
          </AlertDialogCancel>
          <AlertDialogAction
            onClick={(event) => {
              // Keep the dialog up while the request is in flight; the mutation
              // closes it once the temporary password is in hand.
              event.preventDefault();
              handleConfirm();
            }}
            disabled={isPending}
          >
            {isPending ? (
              <>
                <Loader2 className="size-4 animate-spin" />
                {m.resetting}
              </>
            ) : (
              m.confirm
            )}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
