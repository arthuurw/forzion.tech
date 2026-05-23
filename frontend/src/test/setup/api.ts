/**
 * Setup API — env node (vitest project "api").
 *
 * Estende setup/unit com helpers para testes de Next.js Route Handlers.
 *
 * Exporta utilitarios reutilizaveis (createMockRequest, extractCookies)
 * que tests importam quando precisam fabricar NextRequest sem subir um
 * servidor HTTP.
 */
import "./unit";

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

  const request = {
    method: init.method ?? "GET",
    url: init.url ?? "http://localhost:3000/",
    headers,
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
    text: async () =>
      init.body !== undefined ? JSON.stringify(init.body) : "",
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
