/**
 * Setup API — env node (vitest project "api").
 *
 * Estende setup/unit com:
 * - Helpers para testes de Next.js Route Handlers (createMockRequest, extractCookies)
 * - MSW server (route handlers fazem fetch para backend .NET externo)
 *
 * Exporta utilitarios reutilizaveis que tests importam quando precisam
 * fabricar NextRequest sem subir um servidor HTTP.
 */
import "./unit";
import { afterAll, afterEach, beforeAll } from "vitest";
import { server } from "../msw/server";

beforeAll(() => {
  server.listen({ onUnhandledRequest: "error" });
});

afterEach(() => {
  server.resetHandlers();
});

afterAll(() => {
  server.close();
});

import type { NextRequest } from "next/server";

export interface MockRequestInit {
  method?: string;
  url?: string;
  cookies?: Record<string, string>;
  headers?: Record<string, string>;
  body?: unknown;
}

/**
 * Fabrica um NextRequest minimo, suficiente para testar Route Handlers.
 *
 * Nao cobre todos os getters (nextUrl.searchParams completo, geolocation, etc).
 * Estender conforme necessario.
 */
export function createMockRequest(init: MockRequestInit = {}): NextRequest {
  const cookies = init.cookies ?? {};
  const headers = new Headers(init.headers ?? {});
  const urlStr = init.url ?? "http://localhost:3000/";
  const parsedUrl = new URL(urlStr);

  const body = init.body !== undefined ? JSON.stringify(init.body) : "";
  const arrayBuffer = body
    ? new TextEncoder().encode(body).buffer
    : new ArrayBuffer(0);

  const request = {
    method: init.method ?? "GET",
    url: urlStr,
    headers,
    nextUrl: {
      pathname: parsedUrl.pathname,
      search: parsedUrl.search,
      searchParams: parsedUrl.searchParams,
      href: parsedUrl.href,
      origin: parsedUrl.origin,
      clone: () => ({ ...parsedUrl }),
    },
    cookies: {
      get: (name: string) => {
        const value = cookies[name];
        return value !== undefined ? { name, value } : undefined;
      },
      getAll: () =>
        Object.entries(cookies).map(([name, value]) => ({ name, value })),
      has: (name: string) => name in cookies,
    },
    json: async () => (init.body !== undefined ? init.body : {}),
    text: async () => body,
    arrayBuffer: async () => arrayBuffer,
  };

  return request as unknown as NextRequest;
}

/**
 * Extrai cookies do header Set-Cookie de uma Response.
 */
export function extractCookies(response: Response): Record<string, string> {
  const setCookie = response.headers.get("set-cookie");
  if (!setCookie) return {};

  const result: Record<string, string> = {};
  for (const part of setCookie.split(/,(?=[^;]+=)/)) {
    const trimmed = part.trim();
    const eq = trimmed.indexOf("=");
    if (eq < 0) continue;
    const name = trimmed.slice(0, eq);
    const value = trimmed.slice(eq + 1).split(";")[0];
    result[name] = value;
  }
  return result;
}
