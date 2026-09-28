'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { signOut, useSession } from 'next-auth/react';
import { AlertCircle, ArrowRight, Check, Eye, EyeOff, KeyRound, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import { Input } from '@/components/ui/input';
import { messages } from '@/lib/messages.ar';
import { changeMyPassword } from '../../api/auth.api';
import {
  changePasswordSchema,
  type ChangePasswordInput,
} from '../../schemas/change-password.schema';

const m = messages.auth.changePassword;

/* Same glass treatment as the login card — this screen shares the (auth) layout. */
const fieldClassName =
  'h-11 touch-manipulation rounded-xl border-stone-500 bg-white/90 px-3.5 text-stone-900 shadow-sm focus-visible:border-stone-700 focus-visible:ring-3 focus-visible:ring-stone-700/70 disabled:bg-white/60 aria-invalid:border-red-500 aria-invalid:ring-red-500/25 dark:bg-white/90';

interface PasswordFieldProps {
  label: string;
  autoComplete: string;
  disabled: boolean;
  value: string;
  onChange: (value: string) => void;
  onBlur: () => void;
  name: string;
}

function PasswordField({
  label,
  autoComplete,
  disabled,
  value,
  onChange,
  onBlur,
  name,
}: PasswordFieldProps) {
  const [visible, setVisible] = useState(false);

  return (
    <FormItem className="gap-2">
      <FormLabel className="text-sm font-medium text-stone-800">{label}</FormLabel>
      <div className="relative" dir="ltr">
        <FormControl>
          <Input
            name={name}
            value={value}
            onChange={(event) => onChange(event.target.value)}
            onBlur={onBlur}
            type={visible ? 'text' : 'password'}
            autoComplete={autoComplete}
            dir="ltr"
            disabled={disabled}
            className={`${fieldClassName} pe-12`}
          />
        </FormControl>
        <button
          type="button"
          onClick={() => setVisible((current) => !current)}
          disabled={disabled}
          aria-pressed={visible}
          aria-label={visible ? messages.auth.hidePassword : messages.auth.showPassword}
          className="absolute inset-y-0 end-1.5 my-auto flex size-9 touch-manipulation items-center justify-center rounded-lg text-stone-600 transition-colors hover:bg-stone-900/10 hover:text-stone-900 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-stone-700 disabled:pointer-events-none disabled:opacity-50"
        >
          {visible ? (
            <EyeOff className="size-[1.125rem]" aria-hidden />
          ) : (
            <Eye className="size-[1.125rem]" aria-hidden />
          )}
        </button>
      </div>
      <FormMessage className="text-red-700" />
    </FormItem>
  );
}

export function ChangePasswordForm() {
  const { data: session } = useSession();
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const isForced = session?.user?.mustChangePassword === true;

  const form = useForm<ChangePasswordInput>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: {
      currentPassword: '',
      newPassword: '',
      confirmPassword: '',
    },
  });

  async function onSubmit(values: ChangePasswordInput) {
    setError(null);
    setIsSubmitting(true);
    try {
      await changeMyPassword({
        currentPassword: values.currentPassword,
        newPassword: values.newPassword,
      });

      // Changing the password moves the account's session stamp, which retires
      // the token this request was made with. Signing out is not tidiness here,
      // it is the only way back to a working session.
      toast.success(m.success);
      await signOut({ callbackUrl: '/login' });
    } catch (submitError) {
      setError(submitError instanceof Error ? submitError.message : m.error);
      setIsSubmitting(false);
    }
  }

  return (
    <div className="auth-glass w-full rounded-3xl p-7 sm:p-8">
      <div className="mb-7 text-center">
        <div className="mx-auto flex size-14 items-center justify-center rounded-2xl bg-stone-900/10 text-stone-800">
          <KeyRound className="size-6" aria-hidden />
        </div>
        <h1 className="mt-4 text-2xl font-semibold tracking-tight text-stone-900">
          {isForced ? m.forcedTitle : m.title}
        </h1>
        <p className="mt-1.5 text-sm text-stone-600">
          {isForced ? m.forcedDescription : m.description}
        </p>
      </div>

      <Form {...form}>
        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-5" noValidate>
          <FormField
            control={form.control}
            name="currentPassword"
            render={({ field }) => (
              <PasswordField
                label={m.currentPassword}
                autoComplete="current-password"
                disabled={isSubmitting}
                {...field}
              />
            )}
          />

          <FormField
            control={form.control}
            name="newPassword"
            render={({ field }) => (
              <PasswordField
                label={m.newPassword}
                autoComplete="new-password"
                disabled={isSubmitting}
                {...field}
              />
            )}
          />

          <FormField
            control={form.control}
            name="confirmPassword"
            render={({ field }) => (
              <PasswordField
                label={m.confirmPassword}
                autoComplete="new-password"
                disabled={isSubmitting}
                {...field}
              />
            )}
          />

          <div className="rounded-xl border border-stone-400/60 bg-white/60 px-3.5 py-3">
            <p className="text-xs font-medium text-stone-800">{m.requirementsTitle}</p>
            <ul className="mt-2 space-y-1">
              {m.requirements.map((requirement) => (
                <li key={requirement} className="flex items-center gap-2 text-xs text-stone-700">
                  <Check className="size-3.5 shrink-0" aria-hidden />
                  <span>{requirement}</span>
                </li>
              ))}
            </ul>
          </div>

          <div aria-live="polite">
            {error ? (
              <p
                className="flex items-start gap-2 rounded-xl border border-red-300 bg-red-50/90 px-3 py-2.5 text-sm text-red-800"
                role="alert"
              >
                <AlertCircle className="mt-px size-4 shrink-0" aria-hidden />
                <span>{error}</span>
              </p>
            ) : null}
          </div>

          <Button
            type="submit"
            disabled={isSubmitting}
            aria-busy={isSubmitting}
            className="h-11 w-full touch-manipulation rounded-xl bg-stone-900 text-base font-medium text-white shadow-lg transition-colors hover:bg-stone-800 focus-visible:ring-3 focus-visible:ring-stone-900/40 focus-visible:ring-offset-2 disabled:opacity-70 md:text-sm"
          >
            {isSubmitting ? (
              <>
                <Loader2 className="size-4 animate-spin" aria-hidden />
                {m.saving}
              </>
            ) : (
              m.submit
            )}
          </Button>

          {!isForced && (
            <Button
              asChild
              variant="ghost"
              className="h-10 w-full rounded-xl text-stone-700 hover:bg-stone-900/10 hover:text-stone-900"
            >
              <Link href="/dashboard">
                <ArrowRight className="size-4" aria-hidden />
                {m.backToDashboard}
              </Link>
            </Button>
          )}
        </form>
      </Form>
    </div>
  );
}
