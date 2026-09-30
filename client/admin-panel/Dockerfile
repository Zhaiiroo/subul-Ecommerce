FROM node:24-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci

FROM node:24-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
# NEXT_PUBLIC_* values are embedded into browser bundles at build time.
ARG NEXT_PUBLIC_API_URL
RUN test -n "$NEXT_PUBLIC_API_URL"
ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL
ARG NEXT_PUBLIC_IMAGE_URL
ENV NEXT_PUBLIC_IMAGE_URL=$NEXT_PUBLIC_IMAGE_URL
RUN npm run build

FROM node:24-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production
ENV PORT=3000
ENV HOSTNAME=0.0.0.0
COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
COPY --from=build /app/public ./public
# The app files stay root-owned, so the server cannot rewrite its own code. The
# image optimizer's cache is the one path Next writes to at runtime, so only
# that directory belongs to the unprivileged `node` user the image provides.
RUN mkdir -p .next/cache && chown node:node .next/cache
USER node
EXPOSE 3000
# /api/auth/providers, not /: every other path redirects an anonymous caller to
# the login page on the PUBLIC origin (AUTH_URL), which is unreachable from
# inside the container, so following it always failed. This route answers
# directly, and only when Auth.js itself is configured correctly.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD node -e "fetch('http://127.0.0.1:3000/api/auth/providers',{redirect:'manual'}).then(r=>process.exit(r.ok?0:1)).catch(()=>process.exit(1))"
CMD ["node", "server.js"]
