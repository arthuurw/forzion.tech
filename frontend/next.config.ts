import type { NextConfig } from "next";
import path from "node:path";

if (process.env.NODE_ENV === "production" && !process.env.API_BASE_URL) {
  throw new Error("API_BASE_URL is required in production.");
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
    "connect-src 'self' https://api.stripe.com",
    "frame-src https://js.stripe.com",
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

export default nextConfig;
