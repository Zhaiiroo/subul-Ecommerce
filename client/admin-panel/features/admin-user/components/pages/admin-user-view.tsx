'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useSession } from 'next-auth/react';
import { ArrowRight, Copy, KeyRound, Pencil, Trash2, UserRound } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { Separator } from '@/components/ui/separator';
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import { formatDate, messages } from '@/lib/messages.ar';
import { cn } from '@/lib/utils';
import type { AdminUserDto, TemporaryCredential } from '../../types';
import { useAdminUser } from '../../hooks/useAdminUser';
import { useDeleteAdminUser } from '../../hooks/useAdminUserMutations';
import { AdminUserRoleBadge } from '../blocks/admin-user-role-badge';
import { AdminUserStatusBadge } from '../blocks/admin-user-status-badge';
import { AdminUserStatusToggle } from '../blocks/admin-user-status-toggle';
import { AdminUserResetPasswordDialog } from '../blocks/admin-user-reset-password-dialog';
import { TemporaryPasswordDialog } from '../blocks/temporary-password-dialog';

const m = messages.adminUser.view;

function DetailField({
  label,
  value,
  dir,
  className,
}: {
  label: string;
  value: React.ReactNode;
  dir?: 'rtl' | 'ltr' | 'auto';
  className?: string;
}) {
  return (
    <div
      className={cn(
        'space-y-1.5 rounded-lg border border-border/50 bg-muted/20 px-3 py-2.5',
        className,
      )}
    >
      <dt className="text-muted-foreground text-xs font-medium">{label}</dt>
      <dd className="text-sm leading-relaxed" dir={dir}>
        {value}
      </dd>
    </div>
  );
}

function timestamp(value: string | null): string {
  return value ? formatDate(value, { dateStyle: 'medium', timeStyle: 'short' }) : m.never;
}

interface AdminUserViewProps {
  adminUser: AdminUserDto;
}

export function AdminUserView({ adminUser }: AdminUserViewProps) {
  const router = useRouter();
  const { data: session } = useSession();
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [resetOpen, setResetOpen] = useState(false);
  const [credential, setCredential] = useState<TemporaryCredential | null>(null);

  const { data: liveAdminUser } = useAdminUser(adminUser.id, { initialData: adminUser });
  const current = liveAdminUser ?? adminUser;
  const { mutate: deleteAdminUser, isPending } = useDeleteAdminUser();

  const isSelf = session?.user?.id === current.id;

  function handleConfirmDelete() {
    deleteAdminUser(current.id, {
      onSuccess: () => router.push('/admin-users'),
      onSettled: () => setDeleteOpen(false),
    });
  }

  async function handleCopyEmail() {
    try {
      await navigator.clipboard.writeText(current.email);
      toast.success(m.emailCopied);
    } catch {
      toast.error(messages.common.error);
    }
  }

  return (
    <>
      <div className="flex flex-col gap-6">
        <Button
          asChild
          variant="ghost"
          size="sm"
          className="text-muted-foreground hover:text-foreground -ms-2 w-fit gap-1.5"
        >
          <Link href="/admin-users">
            <ArrowRight className="size-4" />
            {m.backToList}
          </Link>
        </Button>

        <Card className="overflow-hidden border-border/60 shadow-xs">
          <CardContent className="flex flex-col gap-5 p-5 sm:p-6">
            <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
              <div className="flex min-w-0 items-start gap-4">
                <div className="bg-primary/10 text-primary flex size-12 shrink-0 items-center justify-center rounded-xl">
                  <UserRound className="size-6" />
                </div>

                <div className="min-w-0 space-y-2">
                  <div className="flex flex-wrap items-center gap-2">
                    <h1 className="text-2xl font-bold tracking-tight">{current.name}</h1>
                    <AdminUserRoleBadge role={current.role} />
                    {isSelf ? (
                      <AdminUserStatusBadge status={current.status} />
                    ) : (
                      <AdminUserStatusToggle
                        adminUserId={current.id}
                        status={current.status}
                      />
                    )}
                  </div>

                  <div className="flex flex-wrap items-center gap-2 pt-1">
                    <code
                      className="bg-muted text-muted-foreground rounded-md px-2 py-1 font-mono text-xs"
                      dir="ltr"
                    >
                      {current.email}
                    </code>
                    <Tooltip>
                      <TooltipTrigger asChild>
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon-xs"
                          onClick={() => void handleCopyEmail()}
                          aria-label={messages.common.copy}
                        >
                          <Copy className="size-3.5" />
                        </Button>
                      </TooltipTrigger>
                      <TooltipContent side="top">{messages.common.copy}</TooltipContent>
                    </Tooltip>
                  </div>

                  {current.mustChangePassword && (
                    <p className="text-muted-foreground inline-flex items-center gap-1.5 text-xs">
                      <KeyRound className="size-3.5" aria-hidden />
                      {messages.adminUser.pendingPasswordChange}
                    </p>
                  )}
                </div>
              </div>

              <div className="flex flex-wrap items-center gap-2">
                <Button variant="outline" onClick={() => setResetOpen(true)}>
                  <KeyRound className="size-4" />
                  {messages.adminUser.reset.action}
                </Button>
                <Button asChild variant="outline">
                  <Link href={`/admin-users/${current.id}/edit`}>
                    <Pencil className="size-4" />
                    {messages.common.edit}
                  </Link>
                </Button>
                {!isSelf && (
                  <Button
                    variant="outline"
                    className="text-destructive hover:bg-destructive/10 hover:text-destructive"
                    onClick={() => setDeleteOpen(true)}
                    disabled={isPending}
                  >
                    <Trash2 className="size-4" />
                    {messages.common.delete}
                  </Button>
                )}
              </div>
            </div>

            <Separator />

            <Card className="shadow-none">
              <CardHeader>
                <CardTitle className="text-base">{m.title}</CardTitle>
              </CardHeader>
              <CardContent>
                <dl className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                  <DetailField label={m.adminUserId} value={current.id} dir="ltr" />
                  <DetailField
                    label={m.role}
                    value={messages.adminUser.role[current.role] ?? current.role}
                  />
                  <DetailField
                    label={m.status}
                    value={messages.adminUser.status[current.status]}
                  />
                  <DetailField label={m.lastLoginAt} value={timestamp(current.lastLoginAt)} />
                  <DetailField
                    label={m.passwordChangedAt}
                    value={timestamp(current.passwordChangedAt)}
                  />
                  <DetailField
                    label={m.createdAt}
                    value={formatDate(current.createdAt, {
                      dateStyle: 'medium',
                      timeStyle: 'short',
                    })}
                  />
                  <DetailField
                    label={m.updatedAt}
                    value={current.updatedAt ? timestamp(current.updatedAt) : m.notUpdated}
                  />
                </dl>
              </CardContent>
            </Card>
          </CardContent>
        </Card>
      </div>

      <AdminUserResetPasswordDialog
        open={resetOpen}
        onOpenChange={setResetOpen}
        adminUserId={current.id}
        adminUserName={current.name}
        onIssued={setCredential}
      />

      <TemporaryPasswordDialog credential={credential} onClose={() => setCredential(null)} />

      <AlertDialog open={deleteOpen} onOpenChange={setDeleteOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{m.deleteTitle}</AlertDialogTitle>
            <AlertDialogDescription>{m.deleteDescription(current.name)}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={isPending}>
              {messages.adminUser.form.cancel}
            </AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              onClick={handleConfirmDelete}
              disabled={isPending}
            >
              {messages.common.delete}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
