import * as Sentry from "@sentry/nextjs";

/**
 * Init do Sentry no runtime Edge (middleware, route handlers edge).
 * Importado por instrumentation.ts. Gated por NEXT_PUBLIC_SENTRY_DSN.
 */
const dsn = process.env.NEXT_PUBLIC_SENTRY_DSN;

Sentry.init({
  dsn,
  enabled: Boolean(dsn),
  environment: process.env.NEXT_PUBLIC_SENTRY_ENV ?? process.env.NODE_ENV,
  tracesSampleRate: Number(process.env.SENTRY_TRACES_SAMPLE_RATE ?? "0.05"),
  sendDefaultPii: false,
  debug: false,
});
