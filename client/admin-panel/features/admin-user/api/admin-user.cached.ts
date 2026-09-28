import { cache } from 'react';
import { getAdminUserById } from './admin-user.api';

/** Per-request deduplication for RSC (metadata + page body). */
export const getCachedAdminUserById = cache(getAdminUserById);
