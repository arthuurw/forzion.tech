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
ARG CSP_REPORT_ONLY=false
ENV CSP_REPORT_ONLY=$CSP_REPORT_ONLY
# Guard de produção no next.config exige JWT_SECRET; placeholder só satisfaz o build
# (este stage é descartado — runtime recebe o valor real via env do container).
# Literal abaixo DEVE ser igual a JWT_SECRET_BUILD_PLACEHOLDER em
# src/lib/auth/buildPlaceholder.ts — buildPlaceholder.test.ts trava drift.
ARG JWT_SECRET=build-placeholder-not-used-at-runtime
ENV JWT_SECRET=$JWT_SECRET
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
