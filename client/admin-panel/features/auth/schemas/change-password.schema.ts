import { z } from 'zod';
import { messages } from '@/lib/messages.ar';

const v = messages.auth.changePassword.validation;

/** Mirrors backend `AdminPasswordPolicy` — keep the two in step. */
export const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, v.currentRequired),
    newPassword: z
      .string()
      .min(10, v.min)
      .max(128, v.max)
      .regex(/[A-Z]/, v.upper)
      .regex(/[a-z]/, v.lower)
      .regex(/[0-9]/, v.digit),
    confirmPassword: z.string().min(1, v.newRequired),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    path: ['confirmPassword'],
    message: v.mismatch,
  })
  .refine((values) => values.newPassword !== values.currentPassword, {
    path: ['newPassword'],
    message: v.sameAsCurrent,
  });

export type ChangePasswordInput = z.infer<typeof changePasswordSchema>;
