import axios from 'axios';
import type { ApiResponse } from '@/types/api';

let _accessToken: string | null = null;
let _accessTokenRequest: Promise<string | null> | null = null;

export function setAxiosToken(token: string | null) {
  _accessToken = token;
}

async function resolveAccessToken(): Promise<string | null> {
  if (_accessToken) return _accessToken;

  // Server Components / SSR: TokenSync never runs, so read JWT from the NextAuth session.
  if (typeof window === 'undefined') {
    try {
      const { auth } = await import('@/auth');
      const session = await auth();
      return session?.user?.accessToken ?? null;
    } catch {
      return null;
    }
  }

  // After a full reload, SessionProvider restores its session asynchronously.
  // Client queries may start before TokenSync has copied the token into this
  // module, so resolve it directly before the first protected API request.
  // Reuse one in-flight lookup when several queries mount at the same time.
  if (!_accessTokenRequest) {
    _accessTokenRequest = import('next-auth/react')
      .then(async ({ getSession }) => {
        const session = await getSession();
        const token = session?.user?.accessToken ?? null;
        if (token) _accessToken = token;
        return token;
      })
      .catch(() => null)
      .finally(() => {
        _accessTokenRequest = null;
      });
  }

  return _accessTokenRequest;
}

const apiBaseUrl = typeof window === 'undefined'
  ? (process.env.INTERNAL_API_URL ?? process.env.NEXT_PUBLIC_API_URL)
  : process.env.NEXT_PUBLIC_API_URL;

const apiClient = axios.create({
  // Server Components run inside Docker, where `localhost` is the admin
  // container. Browsers must keep using the public API origin instead.
  baseURL: apiBaseUrl ?? 'http://localhost:5101/api',
  headers: {
    'Content-Type': 'application/json',
  },
});

apiClient.interceptors.request.use(async (config) => {
  const token = await resolveAccessToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

/**
 * The backend re-checks the account behind every bearer token, so a session can
 * be ended from elsewhere: a superadmin deactivating the account or resetting
 * its password retires the token mid-session (401), and a pending password
 * change closes every route but the change form (403). Neither is something the
 * calling screen can recover from, so both are handled once, here.
 *
 * Server-side callers are left alone — there is no browser to navigate.
 */
async function handleEndedSession(status: number, message: string | undefined) {
  if (typeof window === 'undefined') return;

  if (status === 403 && message?.toLowerCase().includes('password change required')) {
    if (window.location.pathname !== '/change-password') {
      window.location.href = '/change-password';
    }
    return;
  }

  if (status === 401) {
    // The NextAuth cookie outlives the backend token, so clearing it is what
    // stops middleware from bouncing straight back into a dead session.
    const { signOut } = await import('next-auth/react');
    await signOut({ callbackUrl: '/login' });
  }
}

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    const status = axios.isAxiosError(error) ? error.response?.status : undefined;
    const message = axios.isAxiosError(error)
      ? (error.response?.data as { message?: string } | undefined)?.message
      : undefined;
    const authorization = axios.isAxiosError(error)
      ? error.config?.headers?.get?.('Authorization')
      : undefined;
    const hadAccessToken =
      typeof authorization === 'string' && authorization.startsWith('Bearer ');

    // A 401 without a bearer token can be an initialization race or a temporary
    // session lookup failure. Only a rejected token proves that the established
    // browser session must be ended.
    if ((status === 401 && hadAccessToken) || status === 403) {
      void handleEndedSession(status, message);
    }

    return Promise.reject(error);
  },
);

export async function postFormData<T>(path: string, formData: FormData): Promise<T> {
  const { data } = await apiClient.post<ApiResponse<T>>(path, formData, {
    transformRequest: [(payload, headers) => {
      delete headers['Content-Type'];
      return payload;
    }],
  });
  if (!data.success) throw new Error(data.message ?? 'Request failed');
  return data.data!;
}

export default apiClient;
