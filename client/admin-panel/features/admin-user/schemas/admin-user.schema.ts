import { z } from 'zod';

import { messages } from '@/lib/messages.ar';
import {
  ADMIN_USER_PASSWORD_MAX_LENGTH,
  ADMIN_USER_PASSWORD_MIN_LENGTH,
} from '../constants';

const v = messages.adminUser.form.validation;

/** Mirrors backend `AdminPasswordPolicy` — keep the two in step. */
const passwordSchema = z
  .string()
  .min(ADMIN_USER_PASSWORD_MIN_LENGTH, v.passwordMin)
  .max(ADMIN_USER_PASSWORD_MAX_LENGTH, v.passwordMax)
  .regex(/[A-Z]/, v.passwordUpper)
  .regex(/[a-z]/, v.passwordLower)
  .regex(/[0-9]/, v.passwordDigit);

export const createAdminUserSchema = z
  .object({
    name: z.string().min(2, v.nameMin).max(255, v.nameMax),
    email: z
      .string()
      .min(1, v.emailRequired)
      .email(v.emailInvalid)
      .max(255, v.emailMax),
    role: z.enum(['superadmin', 'manager', 'staff'], { message: v.roleRequired }),
    passwordMode: z.enum(['generated', 'manual']),
    password: z.string().optional().or(z.literal('')),
  })
  .superRefine((values, ctx) => {
    if (values.passwordMode !== 'manual') return;

    const result = passwordSchema.safeParse(values.password ?? '');
    if (result.success) return;

    ctx.addIssue({
      code: 'custom',
      path: ['password'],
      message: result.error.issues[0]?.message ?? v.passwordMin,
    });
  });

export type CreateAdminUserInput = z.infer<typeof createAdminUserSchema>;

export const updateAdminUserSchema = z.object({
  name: z.string().min(2, v.nameMin).max(255, v.nameMax),
  email: z
    .string()
    .min(1, v.emailRequired)
    .email(v.emailInvalid)
    .max(255, v.emailMax),
  role: z.enum(['superadmin', 'manager', 'staff'], { message: v.roleRequired }),
});

export type UpdateAdminUserInput = z.infer<typeof updateAdminUserSchema>;

export const adminUserFilterSchema = z.object({
  page: z.coerce.number().int().min(1).optional().default(1),
  limit: z.coerce.number().int().min(1).max(100).optional().default(10),
  search: z.string().max(255).optional(),
  role: z.enum(['superadmin', 'manager', 'staff']).optional(),
  status: z.enum(['active', 'inactive']).optional(),
  sortBy: z.enum(['name', 'email', 'createdAt', 'lastLoginAt']).optional(),
  sortOrder: z.enum(['asc', 'desc']).optional(),
});

export type AdminUserFilterInput = z.infer<typeof adminUserFilterSchema>;
