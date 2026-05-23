import * as Sentry from "@sentry/nextjs";

/**
 * Init do Sentry no browser (Next 15.3+ substitui sentry.client.config).
 *
 * - Replay: 10% das sessoes, 100% quando ha erro (plano §12). maskAllText +
 *   blockAllMedia para nao vazar dados de usuario (LGPD).
 * - Gated por NEXT_PUBLIC_SENTRY_DSN: sem DSN, SDK no-op.
 */
const dsn = process.env.NEXT_PUBLIC_SENTRY_DSN;

Sentry.init({
  dsn,
  enabled: Boolean(dsn),
  environment: process.env.NEXT_PUBLIC_SENTRY_ENV ?? process.env.NODE_ENV,
  tracesSampleRate: Number(process.env.NEXT_PUBLIC_SENTRY_TRACES_SAMPLE_RATE ?? "0.1"),
  replaysSessionSampleRate: Number(process.env.NEXT_PUBLIC_SENTRY_REPLAYS_SESSION_RATE ?? "0.1"),
  replaysOnErrorSampleRate: 1.0,
  sendDefaultPii: false,
  integrations: [Sentry.replayIntegration({ maskAllText: true, blockAllMedia: true })],
});

// Instrumenta transicoes de rota do App Router para tracing de navegacao.
export const onRouterTransitionStart = Sentry.captureRouterTransitionStart;
