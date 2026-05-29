import * as Sentry from "@sentry/nextjs";
import { readConsentCookie } from "./src/hooks/useConsent";

/**
 * Init do Sentry no browser (Next 15.3+ substitui sentry.client.config).
 *
 * - Replay: 10% das sessoes, 100% quando ha erro (plano §12). maskAllText +
 *   blockAllMedia para nao vazar dados de usuario (LGPD).
 * - Gated por NEXT_PUBLIC_SENTRY_DSN: sem DSN, SDK no-op.
 * - Gated por LGPD: so inicializa se o usuario consentiu com analytics
 *   (cookie consent.analytics === true). Padrao OFF.
 */
const dsn = process.env.NEXT_PUBLIC_SENTRY_DSN;

// LGPD: read consent cookie before init. analytics defaults to false (opt-in).
const consentPrefs = readConsentCookie();
const analyticsConsented = consentPrefs?.analytics === true;

Sentry.init({
  dsn,
  enabled: Boolean(dsn) && analyticsConsented,
  environment: process.env.NEXT_PUBLIC_SENTRY_ENV ?? process.env.NODE_ENV,
  tracesSampleRate: Number(process.env.NEXT_PUBLIC_SENTRY_TRACES_SAMPLE_RATE ?? "0.1"),
  replaysSessionSampleRate: Number(process.env.NEXT_PUBLIC_SENTRY_REPLAYS_SESSION_RATE ?? "0.1"),
  replaysOnErrorSampleRate: 1.0,
  sendDefaultPii: false,
  integrations: [Sentry.replayIntegration({ maskAllText: true, blockAllMedia: true })],
});

// Instrumenta transicoes de rota do App Router para tracing de navegacao.
export const onRouterTransitionStart = Sentry.captureRouterTransitionStart;
