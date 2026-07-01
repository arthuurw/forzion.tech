export function buildCsp(isDev: boolean): string {
  return [
    "default-src 'self'",
    // 'unsafe-inline' necessário: Next.js hidratação sem nonce
    // 'unsafe-eval' necessário apenas em dev: MUI Emotion / Next.js hot-reload
    `script-src 'self' 'unsafe-inline'${isDev ? " 'unsafe-eval'" : ""} https://js.stripe.com`,
    "style-src 'self' 'unsafe-inline'", // necessário: Emotion injeta estilos inline
    "img-src 'self' data: blob: https://*.stripe.com https://i.ytimg.com",
    "font-src 'self'",
    // *.sentry.io: ingest de erros/replay/tracing (RUM). No-op sem DSN.
    "connect-src 'self' https://api.stripe.com https://*.sentry.io",
    "frame-src https://js.stripe.com https://www.youtube-nocookie.com",
    // blob:: worker do Sentry Session Replay.
    "worker-src 'self' blob:",
    "frame-ancestors 'none'",
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
  ].join("; ");
}
