import * as Sentry from "@sentry/nextjs";
import { readConsentCookie } from "./src/hooks/useConsent";

/**
 * Init do Sentry no browser (Next 15.3+ substitui sentry.client.config).
 *
 * - Replay: 2% das sessoes (default safe; env-overridable), 100% quando ha erro.
 *   Sample rates ficam aqui no init; a replayIntegration NAO entra no array
 *   estatico (tirava ~62KB do bundle inicial de toda pagina). E adicionada
 *   on-idle via dynamic import por ReplayManager (chunk async, fora do critical
 *   path) com gating de rota/consent. NAO usamos lazyLoadIntegration() porque
 *   ela baixa de CDN Sentry, violando a CSP script-src 'self'.
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
  tracesSampleRate: Number(process.env.NEXT_PUBLIC_SENTRY_TRACES_SAMPLE_RATE ?? "0.05"),
  replaysSessionSampleRate: Number(process.env.NEXT_PUBLIC_SENTRY_REPLAYS_SESSION_RATE ?? "0.02"),
  replaysOnErrorSampleRate: 1.0,
  sendDefaultPii: false,
  integrations: [],
  // OBS-01: propaga o X-Request-Id da ultima resposta do backend como tag no evento
  // Sentry, correlacionando erros de browser com logs estruturados do backend.
  // O valor e gravado em window.__lastRequestId pelo cliente HTTP da app (fetch wrapper)
  // ao receber o header X-Request-Id; beforeSend le sem round-trip adicional.
  beforeSend(event) {
    const requestId =
      typeof window !== "undefined"
        ? (window as typeof window & { __lastRequestId?: string }).__lastRequestId
        : undefined;
    if (requestId) {
      event.tags = { ...event.tags, request_id: requestId };
    }
    return event;
  },
});

// Instrumenta transicoes de rota do App Router para tracing de navegacao.
export const onRouterTransitionStart = Sentry.captureRouterTransitionStart;
