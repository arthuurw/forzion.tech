FROM node:22-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json .npmrc ./
RUN npm ci --omit=dev

FROM node:22-alpine AS builder
WORKDIR /app
COPY package.json package-lock.json .npmrc ./
RUN npm ci
COPY . .
ARG API_BASE_URL=http://backend:8080
ENV API_BASE_URL=$API_BASE_URL
# Guard de produção no next.config exige JWT_SECRET; placeholder só satisfaz o build
# (este stage é descartado — runtime recebe o valor real via env do container).
# Literal abaixo DEVE ser igual a JWT_SECRET_BUILD_PLACEHOLDER em
# src/lib/auth/buildPlaceholder.ts — buildPlaceholder.test.ts trava drift.
ARG JWT_SECRET=build-placeholder-not-used-at-runtime
ENV JWT_SECRET=$JWT_SECRET
# NEXT_PUBLIC_* são congeladas no bundle pelo `next build` — passar via `environment:`
# do compose não tem efeito nenhum no cliente. Toda var nova aqui precisa entrar também
# no build-args do release-images.yml; publicEnvBuildArgs.test.ts trava o drift.
ARG NEXT_PUBLIC_API_BASE_URL
ENV NEXT_PUBLIC_API_BASE_URL=$NEXT_PUBLIC_API_BASE_URL
ARG NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY
ENV NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY=$NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY
ARG NEXT_PUBLIC_SITE_URL
ENV NEXT_PUBLIC_SITE_URL=$NEXT_PUBLIC_SITE_URL
ARG NEXT_PUBLIC_INDEXABLE
ENV NEXT_PUBLIC_INDEXABLE=$NEXT_PUBLIC_INDEXABLE
ARG NEXT_PUBLIC_SENTRY_DSN
ENV NEXT_PUBLIC_SENTRY_DSN=$NEXT_PUBLIC_SENTRY_DSN
ARG NEXT_PUBLIC_SENTRY_ENV
ENV NEXT_PUBLIC_SENTRY_ENV=$NEXT_PUBLIC_SENTRY_ENV
ARG NEXT_PUBLIC_SENTRY_TRACES_SAMPLE_RATE
ENV NEXT_PUBLIC_SENTRY_TRACES_SAMPLE_RATE=$NEXT_PUBLIC_SENTRY_TRACES_SAMPLE_RATE
ARG NEXT_PUBLIC_SENTRY_REPLAYS_SESSION_RATE
ENV NEXT_PUBLIC_SENTRY_REPLAYS_SESSION_RATE=$NEXT_PUBLIC_SENTRY_REPLAYS_SESSION_RATE
RUN npm run build

FROM node:22-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production

RUN addgroup --system nodejs && adduser --system --ingroup nodejs nextjs

COPY --from=builder /app/public ./public
COPY --from=builder --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=builder --chown=nextjs:nodejs /app/.next/static ./.next/static

USER nextjs
EXPOSE 3000
ENV PORT=3000
ENV HOSTNAME=0.0.0.0

CMD ["node", "server.js"]
