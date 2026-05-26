import type { NextConfig } from "next";
import path from "node:path";
import bundleAnalyzerImport from "@next/bundle-analyzer";
import { withSentryConfig } from "@sentry/nextjs";

// @next/bundle-analyzer eh CommonJS — esmModuleInterop normaliza
const withBundleAnalyzer = bundleAnalyzerImport({
  enabled: process.env.ANALYZE === "true",
});

if (process.env.NODE_ENV === "production" && !process.env.API_BASE_URL) {
  throw new Error("API_BASE_URL is required in production.");
}
if (process.env.NODE_ENV === "production" && !process.env.JWT_SECRET) {
  throw new Error("JWT_SECRET is required in production.");
}

const isDev = process.env.NODE_ENV === "development";

const buildCsp = () =>
  [
    "default-src 'self'",
    // 'unsafe-inline' necessário: Next.js hidratação sem nonce
    // 'unsafe-eval' necessário apenas em dev: MUI Emotion / Next.js hot-reload
    `script-src 'self' 'unsafe-inline'${isDev ? " 'unsafe-eval'" : ""} https://js.stripe.com`,
    "style-src 'self' 'unsafe-inline'",                // necessário: Emotion injeta estilos inline
    "img-src 'self' data: blob: https://*.stripe.com",
    "font-src 'self'",
    // *.sentry.io: ingest de erros/replay/tracing (RUM). No-op sem DSN.
    "connect-src 'self' https://api.stripe.com https://*.sentry.io",
    "frame-src https://js.stripe.com",
    // blob:: worker do Sentry Session Replay.
    "worker-src 'self' blob:",
    "frame-ancestors 'none'",
    "base-uri 'self'",
    "form-action 'self'",
  ].join("; ");

const securityHeaders = [
  { key: "X-DNS-Prefetch-Control",       value: "on" },
  { key: "X-Frame-Options",              value: "DENY" },
  { key: "X-Content-Type-Options",       value: "nosniff" },
  { key: "Referrer-Policy",              value: "strict-origin-when-cross-origin" },
  { key: "Permissions-Policy",           value: "camera=(), microphone=(), geolocation=()" },
  { key: "Strict-Transport-Security",    value: "max-age=31536000; includeSubDomains" },
  { key: "Content-Security-Policy",      value: buildCsp() },
  // CSP_REPORT_ONLY=true em homolog: captura violações sem bloquear, útil para detectar regressões de CSP antes de prod
  ...(process.env.CSP_REPORT_ONLY === "true" ? [{ key: "Content-Security-Policy-Report-Only", value: buildCsp() }] : []),
];

const nextConfig: NextConfig = {
  output: "standalone",
  turbopack: {
    root: path.resolve(__dirname),
  },
  experimental: {
    optimizePackageImports: ["@mui/material", "@mui/icons-material"],
  },
  async headers() {
    return [{ source: "/(.*)", headers: securityHeaders }];
  },
};

/**
 * withSentryConfig: injeta o plugin de build do Sentry (source maps + tunelamento
 * opcional). O upload de source maps so ocorre quando SENTRY_AUTH_TOKEN existe,
 * entao `next build` em dev/CI sem token funciona normal (sem upload).
 */
export default withSentryConfig(withBundleAnalyzer(nextConfig), {
  org: process.env.SENTRY_ORG,
  project: process.env.SENTRY_PROJECT,
  authToken: process.env.SENTRY_AUTH_TOKEN,
  silent: !process.env.CI,
  widenClientFileUpload: true,
  disableLogger: true,
  sourcemaps: { disable: !process.env.SENTRY_AUTH_TOKEN },
});
