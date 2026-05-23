import { http } from "msw";
import type { HttpHandler } from "msw";

/**
 * Handlers MSW para endpoints administrativos.
 *
 * Tipos disponiveis em src/test/msw/types.ts (gerado de OpenAPI).
 * Adicionar handlers conforme tests migrarem de vi.mock(axios) na Fase 5.
 */
export const adminHandlers: HttpHandler[] = [
  // Exemplo - sera populado durante Fase 5:
  // http.get("*/admin/alunos", () => HttpResponse.json({ data: [] })),
];

// Suprime warning de export nao usado enquanto stubs vazios.
void http;
