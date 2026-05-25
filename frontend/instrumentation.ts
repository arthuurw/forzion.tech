import * as Sentry from "@sentry/nextjs";

/**
 * Hook de instrumentacao do Next (server). Carrega o init do Sentry conforme
 * o runtime ativo. `onRequestError` reporta erros de Server Components,
 * route handlers e SSR ao Sentry (Next 15+).
 */
export async function register() {
  if (process.env.NEXT_RUNTIME === "nodejs") {
    await import("./sentry.server.config");
  }
  if (process.env.NEXT_RUNTIME === "edge") {
    await import("./sentry.edge.config");
  }
}

export const onRequestError = Sentry.captureRequestError;
