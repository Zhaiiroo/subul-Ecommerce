import NextAuth from 'next-auth';
import { CredentialsSignin } from 'next-auth';
import Credentials from 'next-auth/providers/credentials';

const BACKEND_API =
  // Auth.js runs on the Next.js server, which is inside the `admin` container.
  // Use Docker's service address there; `NEXT_PUBLIC_API_URL` is for browsers.
  process.env.INTERNAL_API_URL ??
  process.env.NEXT_PUBLIC_API_URL ??
  'http://localhost:5101/api';

class RateLimitedCredentialsSignin extends CredentialsSignin {
  constructor(retryAfterSeconds: number) {
    super();
    this.code = `rate-limited-${retryAfterSeconds}`;
  }
}

export const { handlers, signIn, signOut, auth } = NextAuth({
  providers: [
    Credentials({
      credentials: {
        email: { label: 'Email', type: 'email' },
        password: { label: 'Password', type: 'password' },
      },
      async authorize(credentials) {
        const res = await fetch(`${BACKEND_API}/auth/login`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            email: credentials?.email,
            password: credentials?.password,
          }),
        });

        if (res.status === 429) {
          const retryAfterSeconds = Number.parseInt(
            res.headers.get('Retry-After') ?? '',
            10,
          );
          throw new RateLimitedCredentialsSignin(
            Number.isFinite(retryAfterSeconds) && retryAfterSeconds > 0
              ? retryAfterSeconds
              : 300,
          );
        }

        if (!res.ok) return null;

        const json = await res.json();
        if (!json.success) return null;

        // `user` carries mustChangePassword: the backend closes every route but
        // change-password while it is set, and middleware.ts routes there.
        const { accessToken, user } = json.data;
        return { ...user, accessToken };
      },
    }),
  ],

  session: { strategy: 'jwt', maxAge: 8 * 60 * 60 },

  callbacks: {
    jwt({ token, user }) {
      if (user) {
        token.accessToken = (user as { accessToken: string }).accessToken;
        token.role = (user as { role: string }).role;
        token.id = (user as { id: number }).id;
        token.mustChangePassword =
          (user as { mustChangePassword?: boolean }).mustChangePassword ?? false;
      }
      return token;
    },
    session({ session, token }) {
      return {
        ...session,
        user: {
          ...session.user,
          id: token.id as number,
          role: token.role as string,
          accessToken: token.accessToken as string,
          mustChangePassword: token.mustChangePassword === true,
        },
      };
    },
  },

  pages: {
    signIn: '/login',
  },
});
