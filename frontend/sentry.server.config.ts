import * as Sentry from "@sentry/nextjs";

/**
 * Init do Sentry no runtime Node (server). Importado por instrumentation.ts.
 *
 * Gated por NEXT_PUBLIC_SENTRY_DSN: sem DSN o SDK fica desabilitado (no-op),
 * entao dev e CI funcionam sem nenhuma config de observabilidade.
 */
const dsn = process.env.NEXT_PUBLIC_SENTRY_DSN;

Sentry.init({
  dsn,
  enabled: Boolean(dsn),
  environment: process.env.NEXT_PUBLIC_SENTRY_ENV ?? process.env.NODE_ENV,
  tracesSampleRate: Number(process.env.SENTRY_TRACES_SAMPLE_RATE ?? "0.05"),
  // Privacidade/LGPD: nunca anexar PII (IP, cookies, headers) por padrao.
  sendDefaultPii: false,
  debug: false,
});
