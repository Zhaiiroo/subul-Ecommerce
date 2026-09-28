import type { Metadata } from 'next';
import { ChangePasswordForm } from '@/features/auth';
import { messages } from '@/lib/messages.ar';

export const metadata: Metadata = {
  title: messages.auth.changePassword.title,
};

export default function ChangePasswordPage() {
  return <ChangePasswordForm />;
}
