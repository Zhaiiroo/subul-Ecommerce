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
# Public origin used for canonical and Open Graph URLs.
ARG NEXT_PUBLIC_SITE_URL
ENV NEXT_PUBLIC_SITE_URL=$NEXT_PUBLIC_SITE_URL
ARG NEXT_PUBLIC_IMAGE_URL
ENV NEXT_PUBLIC_IMAGE_URL=$NEXT_PUBLIC_IMAGE_URL
# Where the SERVER reaches the API. Needed at build time, not just at runtime:
# next.config's rewrites() is evaluated during the build and frozen into
# routes-manifest.json, so setting this only as a runtime variable has no effect.
ARG INTERNAL_API_ORIGIN
ENV INTERNAL_API_ORIGIN=$INTERNAL_API_ORIGIN
RUN npm run build

FROM node:24-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production
ENV PORT=3001
ENV HOSTNAME=0.0.0.0
COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
COPY --from=build /app/public ./public
# The app files stay root-owned, so the server cannot rewrite its own code. The
# image optimizer's cache is the one path Next writes to at runtime, so only
# that directory belongs to the unprivileged `node` user the image provides.
RUN mkdir -p .next/cache && chown node:node .next/cache
USER node
EXPOSE 3001
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD node -e "fetch('http://127.0.0.1:3001').then(r=>process.exit(r.status<500?0:1)).catch(()=>process.exit(1))"
CMD ["node", "server.js"]
