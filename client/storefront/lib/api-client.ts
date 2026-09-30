import axios from 'axios';

const publicApiUrl = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5101/api';

/**
 * The browser and the server reach the API at different addresses once a proxy
 * sits in front: the browser uses the public origin, while server components
 * run inside the container network, where that origin resolves to the
 * container's own loopback and the request is refused.
 *
 * INTERNAL_API_ORIGIN (no NEXT_PUBLIC_ prefix, so it stays server-side and is
 * read at request time) names the in-network address. Unset, this falls back to
 * the public URL, which is correct when there is no proxy — `next dev` on a
 * workstation, or a deployment where both addresses are the same.
 */
function resolveBaseUrl(): string {
  if (typeof window !== 'undefined') return publicApiUrl;

  const internalOrigin = process.env.INTERNAL_API_ORIGIN;
  return internalOrigin ? `${internalOrigin.replace(/\/$/, '')}/api` : publicApiUrl;
}

const apiClient = axios.create({
  baseURL: resolveBaseUrl(),
  headers: {
    'Content-Type': 'application/json',
  },
  // ASP.NET binds a List<T> from repeated keys (`brandIds=1&brandIds=2`).
  // Axios defaults to `brandIds[]=1`, which the model binder ignores outright —
  // the filter then silently does nothing instead of failing.
  paramsSerializer: { indexes: null },
});

/**
 * While a page renders on the server, every API call leaves from this
 * container, so the API would see one caller for the whole site and hold every
 * visitor to a single shared rate-limit bucket. Forwarding the visitor's
 * X-Forwarded-For (set by Traefik) lets the API, which trusts this container's
 * fixed address, attribute each call to the visitor it is made for.
 *
 * The header is passed on unchanged: the API reads only the last entry, the one
 * Traefik appended, so anything a client put in front of it is ignored.
 */
if (typeof window === 'undefined') {
  apiClient.interceptors.request.use(async (config) => {
    const forwardedFor = await readVisitorForwardedFor();
    if (forwardedFor) config.headers.set('X-Forwarded-For', forwardedFor);
    return config;
  });
}

async function readVisitorForwardedFor(): Promise<string | null> {
  try {
    const { headers } = await import('next/headers');
    return (await headers()).get('x-forwarded-for');
  } catch (error) {
    // Rethrows Next's own control-flow errors so rendering behaves as if this
    // were not here; anything else means there is no request to read — a call
    // outside a render, such as at build time — and the call goes unattributed.
    const { unstable_rethrow } = await import('next/navigation');
    unstable_rethrow(error);
    return null;
  }
}

export default apiClient;
