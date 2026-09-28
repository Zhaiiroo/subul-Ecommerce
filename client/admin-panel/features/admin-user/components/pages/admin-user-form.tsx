'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useForm, type Resolver } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
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
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { FormActionsBar } from '@/components/layout/form-actions-bar';
import { Loader2, Save, X } from 'lucide-react';
import { messages } from '@/lib/messages.ar';
import {
  createAdminUserSchema,
  type CreateAdminUserInput,
} from '../../schemas/admin-user.schema';
import { useCreateAdminUser, useUpdateAdminUser } from '../../hooks/useAdminUserMutations';
import { TemporaryPasswordDialog } from '../blocks/temporary-password-dialog';
import type { AdminUserDto, AdminUserRole, TemporaryCredential } from '../../types';

const m = messages.adminUser.form;
const roleLabels = messages.adminUser.role;
const roleHelp = messages.adminUser.roleHelp;

function getDefaultValues(initialData?: AdminUserDto | null): CreateAdminUserInput {
  if (initialData) {
    return {
      name: initialData.name,
      email: initialData.email,
      role: initialData.role,
      passwordMode: 'generated',
      password: '',
    };
  }

  return {
    name: '',
    email: '',
    role: 'staff',
    passwordMode: 'generated',
    password: '',
  };
}

interface AdminUserFormProps {
  initialData?: AdminUserDto | null;
}

export function AdminUserForm({ initialData }: AdminUserFormProps) {
  const router = useRouter();
  const { mutateAsync: createAdminUserAsync, isPending: isCreating } = useCreateAdminUser();
  const { mutateAsync: updateAdminUserAsync, isPending: isUpdating } = useUpdateAdminUser();
  const isPending = isCreating || isUpdating;
  const isEditMode = !!initialData;
  const [leaveOpen, setLeaveOpen] = useState(false);
  const [credential, setCredential] = useState<TemporaryCredential | null>(null);

  const form = useForm<CreateAdminUserInput>({
    resolver: zodResolver(createAdminUserSchema) as Resolver<CreateAdminUserInput>,
    defaultValues: getDefaultValues(initialData),
  });

  const { isDirty } = form.formState;
  const passwordMode = form.watch('passwordMode');

  useEffect(() => {
    if (initialData) {
      form.reset(getDefaultValues(initialData));
    }
  }, [initialData, form.reset]);

  useEffect(() => {
    if (!isDirty) return;
    const onBeforeUnload = (e: BeforeUnloadEvent) => {
      e.preventDefault();
    };
    window.addEventListener('beforeunload', onBeforeUnload);
    return () => window.removeEventListener('beforeunload', onBeforeUnload);
  }, [isDirty]);

  async function onSubmit(values: CreateAdminUserInput) {
    try {
      if (isEditMode && initialData) {
        await updateAdminUserAsync({
          id: initialData.id,
          payload: {
            name: values.name.trim(),
            email: values.email.trim().toLowerCase(),
            role: values.role as AdminUserRole,
          },
        });
        router.push('/admin-users');
        return;
      }

      const result = await createAdminUserAsync({
        name: values.name.trim(),
        email: values.email.trim().toLowerCase(),
        role: values.role as AdminUserRole,
        password: values.passwordMode === 'manual' ? values.password || undefined : undefined,
      });

      if (result.temporaryPassword) {
        // Hold the form open until the password has been seen — navigating away
        // first would lose it, and it cannot be read back.
        form.reset(getDefaultValues());
        setCredential({
          adminUserId: result.adminUser.id,
          name: result.adminUser.name,
          email: result.adminUser.email,
          temporaryPassword: result.temporaryPassword,
        });
        return;
      }

      router.push('/admin-users');
    } catch {
      // toast handled in mutation hooks
    }
  }

  function handleCancel() {
    if (isDirty) {
      setLeaveOpen(true);
      return;
    }
    router.push('/admin-users');
  }

  return (
    <>
      <Form {...form}>
        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
          <div className="grid gap-6 lg:grid-cols-[1fr_300px]">
            <div className="space-y-6">
              <Card className="shadow-xs">
                <CardHeader>
                  <CardTitle className="text-base">{m.sections.identity}</CardTitle>
                </CardHeader>
                <CardContent className="grid gap-4">
                  <FormField
                    control={form.control}
                    name="name"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>{m.name} *</FormLabel>
                        <FormControl>
                          <Input
                            id="admin-user-name"
                            placeholder={m.namePlaceholder}
                            {...field}
                            disabled={isPending}
                          />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />

                  <FormField
                    control={form.control}
                    name="email"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>{m.email} *</FormLabel>
                        <FormControl>
                          <Input
                            id="admin-user-email"
                            type="email"
                            placeholder={m.emailPlaceholder}
                            dir="ltr"
                            autoComplete="off"
                            {...field}
                            disabled={isPending}
                          />
                        </FormControl>
                        <FormDescription>{m.emailHelp}</FormDescription>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                </CardContent>
              </Card>

              {!isEditMode && (
                <Card className="shadow-xs">
                  <CardHeader>
                    <CardTitle className="text-base">{m.sections.password}</CardTitle>
                  </CardHeader>
                  <CardContent className="grid gap-4">
                    <FormField
                      control={form.control}
                      name="passwordMode"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>{m.passwordMode}</FormLabel>
                          <Select
                            onValueChange={field.onChange}
                            value={field.value}
                            disabled={isPending}
                          >
                            <FormControl>
                              <SelectTrigger id="admin-user-password-mode">
                                <SelectValue />
                              </SelectTrigger>
                            </FormControl>
                            <SelectContent>
                              <SelectItem value="generated">
                                {m.passwordModeGenerated}
                              </SelectItem>
                              <SelectItem value="manual">{m.passwordModeManual}</SelectItem>
                            </SelectContent>
                          </Select>
                          <FormDescription>{m.passwordModeHelp}</FormDescription>
                          <FormMessage />
                        </FormItem>
                      )}
                    />

                    {passwordMode === 'manual' && (
                      <FormField
                        control={form.control}
                        name="password"
                        render={({ field }) => (
                          <FormItem>
                            <FormLabel>{m.password} *</FormLabel>
                            <FormControl>
                              <Input
                                id="admin-user-password"
                                type="password"
                                placeholder={m.passwordPlaceholder}
                                dir="ltr"
                                autoComplete="new-password"
                                {...field}
                                value={field.value ?? ''}
                                disabled={isPending}
                              />
                            </FormControl>
                            <FormMessage />
                          </FormItem>
                        )}
                      />
                    )}
                  </CardContent>
                </Card>
              )}
            </div>

            <div className="space-y-6">
              <Card className="shadow-xs">
                <CardHeader>
                  <CardTitle className="text-base">{m.sections.access}</CardTitle>
                </CardHeader>
                <CardContent className="space-y-4">
                  <FormField
                    control={form.control}
                    name="role"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>{m.role} *</FormLabel>
                        <Select
                          onValueChange={field.onChange}
                          value={field.value}
                          disabled={isPending}
                        >
                          <FormControl>
                            <SelectTrigger id="admin-user-role">
                              <SelectValue placeholder={m.rolePlaceholder} />
                            </SelectTrigger>
                          </FormControl>
                          <SelectContent>
                            <SelectItem value="superadmin">{roleLabels.superadmin}</SelectItem>
                            <SelectItem value="manager">{roleLabels.manager}</SelectItem>
                            <SelectItem value="staff">{roleLabels.staff}</SelectItem>
                          </SelectContent>
                        </Select>
                        <FormDescription>
                          {roleHelp[field.value as AdminUserRole] ?? roleHelp.staff}
                        </FormDescription>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                </CardContent>
              </Card>
            </div>
          </div>

          <FormActionsBar>
            <Button
              id="admin-user-submit-btn"
              type="submit"
              disabled={isPending}
              className="min-w-[130px]"
            >
              {isPending ? (
                <>
                  <Loader2 className="size-4 animate-spin" />
                  {m.saving}
                </>
              ) : (
                <>
                  <Save className="size-4" />
                  {isEditMode ? m.update : m.create}
                </>
              )}
            </Button>
            <Button
              id="admin-user-cancel-btn"
              type="button"
              variant="outline"
              disabled={isPending}
              onClick={handleCancel}
            >
              <X className="size-4" />
              {m.cancel}
            </Button>
          </FormActionsBar>
        </form>
      </Form>

      <TemporaryPasswordDialog
        credential={credential}
        onClose={() => {
          setCredential(null);
          router.push('/admin-users');
        }}
      />

      <AlertDialog open={leaveOpen} onOpenChange={setLeaveOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{m.unsavedTitle}</AlertDialogTitle>
            <AlertDialogDescription>{m.unsavedDescription}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{m.unsavedStay}</AlertDialogCancel>
            <AlertDialogAction onClick={() => router.push('/admin-users')}>
              {m.unsavedLeave}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
