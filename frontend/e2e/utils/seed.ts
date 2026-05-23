import type { APIRequestContext } from "@playwright/test";

/**
 * Stub de seed do backend. Fase 10 popula com endpoints reais (test-only API)
 * ou substitui por fixtures determinanisticas. Por enquanto, expose API:
 *
 *   await seedAluno(request, { email: "test@x.com", senha: "..." });
 *
 * Implementacao default lanca erro pra forcar decisao consciente em Fase 10.
 */

export async function seedAluno(_request: APIRequestContext, _data: unknown): Promise<void> {
  throw new Error(
    "seedAluno nao implementado — Fase 10 deve definir endpoint test-only ou usar API real",
  );
}

export async function seedTreinador(_request: APIRequestContext, _data: unknown): Promise<void> {
  throw new Error("seedTreinador nao implementado — pending Fase 10");
}

export async function cleanupTestData(_request: APIRequestContext, _ids: string[]): Promise<void> {
  throw new Error("cleanupTestData nao implementado — pending Fase 10");
}
