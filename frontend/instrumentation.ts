import * as Sentry from "@sentry/nextjs";

/**
 * Hook de instrumentacao do Next (server). Carrega o init do Sentry conforme
 * o runtime ativo. `onRequestError` reporta erros de Server Components,
 * route handlers e SSR ao Sentry (Next 15+).
 */
export async function register() {
  if (process.env.NEXT_RUNTIME === "nodejs") {
    // Fail-fast de runtime: /api/auth/me valida a assinatura do JWT com JWT_SECRET.
    // Em produção ele precisa ser o segredo real do backend (Auth__JwtSecret), não
    // o placeholder de build do Dockerfile. Sem isso, jwtVerify falha e a sessão
    // nunca hidrata — falha silenciosa. O guard do next.config só roda no build
    // (modo standalone), então a checagem de runtime precisa morar aqui.
    if (process.env.NODE_ENV === "production") {
      const secret = process.env.JWT_SECRET;
      if (!secret || secret === "build-placeholder-not-used-at-runtime") {
        throw new Error(
          "JWT_SECRET ausente ou igual ao placeholder de build em runtime de produção. " +
            "Defina JWT_SECRET (mesmo valor de Auth__JwtSecret do backend) no ambiente do serviço frontend.",
        );
      }
    }
    await import("./sentry.server.config");
  }
  if (process.env.NEXT_RUNTIME === "edge") {
    await import("./sentry.edge.config");
  }
}

export const onRequestError = Sentry.captureRequestError;
