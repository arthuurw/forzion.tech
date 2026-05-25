import { http, HttpResponse } from "msw";
import type { HttpHandler } from "msw";

/**
 * Handlers MSW para Next.js API routes de auth.
 *
 * Esses sao routes /api/auth/* do PROPRIO Next (nao backend .NET).
 * MSW intercepta fetch independente de origem.
 *
 * Defaults retornam 401 (nao autenticado) — testes que precisam de sessao
 * ativa usam server.use(...) com override.
 */
export const authHandlers: HttpHandler[] = [
  http.get("*/api/auth/me", () => HttpResponse.json(null, { status: 401 })),
  http.post("*/api/auth/logout", () => HttpResponse.json({ ok: true })),
];
