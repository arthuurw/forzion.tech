/**
 * Contratos consumer-driven — error paths (F5 fase 3 + F5b fase 4 test
 * remediation).
 *
 * Espelha consumer.test.ts (happy 200) pra status 401/404/500 nos 4 endpoints
 * cobertos. Garante que o backend mantem shape estavel de ProblemDetails (RFC
 * 7807) — mudanca de title/detail/code quebra o Pact.
 *
 * Shape esperado (vide forzion.tech.Api/Middleware/GlobalExceptionHandler.cs):
 *   { status: number, title: string, detail: string, instance: string,
 *     [code?]: string }
 *
 * O apiClient lanca AxiosError em non-2xx; o consumer le err.response.data.
 *
 * ── Publicacao ───────────────────────────────────────────────────────────────
 * F5b (fase 4): provider state handlers implementados em
 * ForzionApiProviderTests.cs — o middleware ProviderStateMiddleware mapeia
 * states em ProblemDetails matching. Contratos agora publicam pro broker
 * principal (`pacts/`); CI verifica via pact-provider workflow.
 */
import { afterEach, describe, expect, it } from "vitest";
import { PactV3, MatchersV3 } from "@pact-foundation/pact";
import type { AxiosError } from "axios";
import { apiClient } from "@/lib/api/client";
import { alunoApi } from "@/lib/api/aluno";
import { contaApi } from "@/lib/api/conta";
import { adminApi } from "@/lib/api/admin";
import { CONSUMER, PROVIDER, PACT_DIR } from "./support/pact-config";

const { like, integer, string } = MatchersV3;

const originalBaseURL = apiClient.defaults.baseURL;
afterEach(() => {
  apiClient.defaults.baseURL = originalBaseURL;
});

function newPact() {
  return new PactV3({ consumer: CONSUMER, provider: PROVIDER, dir: PACT_DIR, logLevel: "warn" });
}

function problemDetails(status: number, title: string, instance: string) {
  return like({
    status: integer(status),
    title: string(title),
    detail: string("erro representativo"),
    instance: string(instance),
  });
}

/**
 * Wrapper: aciona handler do consumer e captura o AxiosError thrown pra
 * inspecionar status + body. Falha duro se a chamada nao throw (defesa contra
 * regression onde o consumer engole o erro).
 */
async function captureError(call: () => Promise<unknown>): Promise<AxiosError> {
  try {
    await call();
    throw new Error("Chamada deveria ter lancado erro (status nao-2xx)");
  } catch (err) {
    const axiosError = err as AxiosError;
    if (!axiosError.response) {
      throw err;
    }
    return axiosError;
  }
}

describe("Contrato: forzion-frontend -> forzion-api (errors)", () => {
  // ── 401 Unauthorized ─────────────────────────────────────────────────────────

  it("GET /aluno/fichas -> 401 quando nao autenticado", async () => {
    const pact = newPact()
      .given("requisicao sem credenciais validas")
      .uponReceiving("listagem de fichas sem auth")
      .withRequest({ method: "GET", path: "/aluno/fichas" })
      .willRespondWith({ status: 401, body: problemDetails(401, "Não autorizado", "/aluno/fichas") });

    await pact.executeTest(async (mock) => {
      apiClient.defaults.baseURL = mock.url;
      const err = await captureError(() => alunoApi.listFichas());
      expect(err.response?.status).toBe(401);
      expect((err.response?.data as { title: string }).title).toBeTruthy();
    });
  });

  it("GET /aluno/vinculo -> 401 quando nao autenticado", async () => {
    const pact = newPact()
      .given("requisicao sem credenciais validas")
      .uponReceiving("consulta de vinculo sem auth")
      .withRequest({ method: "GET", path: "/aluno/vinculo" })
      .willRespondWith({ status: 401, body: problemDetails(401, "Não autorizado", "/aluno/vinculo") });

    await pact.executeTest(async (mock) => {
      apiClient.defaults.baseURL = mock.url;
      const err = await captureError(() => alunoApi.getMeuVinculo());
      expect(err.response?.status).toBe(401);
    });
  });

  it("GET /conta/perfil -> 401 quando nao autenticado", async () => {
    const pact = newPact()
      .given("requisicao sem credenciais validas")
      .uponReceiving("consulta de perfil sem auth")
      .withRequest({ method: "GET", path: "/conta/perfil" })
      .willRespondWith({ status: 401, body: problemDetails(401, "Não autorizado", "/conta/perfil") });

    await pact.executeTest(async (mock) => {
      apiClient.defaults.baseURL = mock.url;
      const err = await captureError(() => contaApi.getPerfil());
      expect(err.response?.status).toBe(401);
    });
  });

  it("GET /admin/alunos -> 401 quando nao autenticado como admin", async () => {
    const pact = newPact()
      .given("requisicao sem perfil de admin")
      .uponReceiving("listagem de alunos admin sem auth")
      .withRequest({ method: "GET", path: "/admin/alunos" })
      .willRespondWith({ status: 401, body: problemDetails(401, "Não autorizado", "/admin/alunos") });

    await pact.executeTest(async (mock) => {
      apiClient.defaults.baseURL = mock.url;
      const err = await captureError(() => adminApi.listAlunos());
      expect(err.response?.status).toBe(401);
    });
  });

  // ── 404 NotFound ─────────────────────────────────────────────────────────────

  it("GET /aluno/fichas -> 404 quando aluno nao encontrado", async () => {
    const pact = newPact()
      .given("aluno autenticado mas nao existe no banco")
      .uponReceiving("listagem de fichas com aluno inexistente")
      .withRequest({ method: "GET", path: "/aluno/fichas" })
      .willRespondWith({ status: 404, body: problemDetails(404, "Não encontrado", "/aluno/fichas") });

    await pact.executeTest(async (mock) => {
      apiClient.defaults.baseURL = mock.url;
      const err = await captureError(() => alunoApi.listFichas());
      expect(err.response?.status).toBe(404);
    });
  });

  it("GET /aluno/vinculo -> 404 quando aluno nao encontrado", async () => {
    const pact = newPact()
      .given("aluno autenticado mas sem registro")
      .uponReceiving("consulta de vinculo com aluno inexistente")
      .withRequest({ method: "GET", path: "/aluno/vinculo" })
      .willRespondWith({ status: 404, body: problemDetails(404, "Não encontrado", "/aluno/vinculo") });

    await pact.executeTest(async (mock) => {
      apiClient.defaults.baseURL = mock.url;
      const err = await captureError(() => alunoApi.getMeuVinculo());
      expect(err.response?.status).toBe(404);
    });
  });

  it("GET /conta/perfil -> 404 quando perfil nao encontrado", async () => {
    const pact = newPact()
      .given("conta autenticada mas sem perfil")
      .uponReceiving("consulta de perfil sem registro de perfil")
      .withRequest({ method: "GET", path: "/conta/perfil" })
      .willRespondWith({ status: 404, body: problemDetails(404, "Não encontrado", "/conta/perfil") });

    await pact.executeTest(async (mock) => {
      apiClient.defaults.baseURL = mock.url;
      const err = await captureError(() => contaApi.getPerfil());
      expect(err.response?.status).toBe(404);
    });
  });

  it("GET /admin/alunos -> 404 quando recurso inexistente", async () => {
    const pact = newPact()
      .given("admin autenticado em recurso ausente")
      .uponReceiving("listagem de alunos com filtro inexistente")
      .withRequest({ method: "GET", path: "/admin/alunos" })
      .willRespondWith({ status: 404, body: problemDetails(404, "Não encontrado", "/admin/alunos") });

    await pact.executeTest(async (mock) => {
      apiClient.defaults.baseURL = mock.url;
      const err = await captureError(() => adminApi.listAlunos());
      expect(err.response?.status).toBe(404);
    });
  });

  // ── 500 InternalServerError ──────────────────────────────────────────────────

  it("GET /aluno/fichas -> 500 em falha inesperada", async () => {
    const pact = newPact()
      .given("falha inesperada no backend")
      .uponReceiving("listagem de fichas com erro 5xx")
      .withRequest({ method: "GET", path: "/aluno/fichas" })
      .willRespondWith({ status: 500, body: problemDetails(500, "Erro interno", "/aluno/fichas") });

    await pact.executeTest(async (mock) => {
      apiClient.defaults.baseURL = mock.url;
      const err = await captureError(() => alunoApi.listFichas());
      expect(err.response?.status).toBe(500);
    });
  });

  it("GET /aluno/vinculo -> 500 em falha inesperada", async () => {
    const pact = newPact()
      .given("falha inesperada no backend")
      .uponReceiving("consulta de vinculo com erro 5xx")
      .withRequest({ method: "GET", path: "/aluno/vinculo" })
      .willRespondWith({ status: 500, body: problemDetails(500, "Erro interno", "/aluno/vinculo") });

    await pact.executeTest(async (mock) => {
      apiClient.defaults.baseURL = mock.url;
      const err = await captureError(() => alunoApi.getMeuVinculo());
      expect(err.response?.status).toBe(500);
    });
  });

  it("GET /conta/perfil -> 500 em falha inesperada", async () => {
    const pact = newPact()
      .given("falha inesperada no backend")
      .uponReceiving("consulta de perfil com erro 5xx")
      .withRequest({ method: "GET", path: "/conta/perfil" })
      .willRespondWith({ status: 500, body: problemDetails(500, "Erro interno", "/conta/perfil") });

    await pact.executeTest(async (mock) => {
      apiClient.defaults.baseURL = mock.url;
      const err = await captureError(() => contaApi.getPerfil());
      expect(err.response?.status).toBe(500);
    });
  });

  it("GET /admin/alunos -> 500 em falha inesperada", async () => {
    const pact = newPact()
      .given("falha inesperada no backend")
      .uponReceiving("listagem de alunos admin com erro 5xx")
      .withRequest({ method: "GET", path: "/admin/alunos" })
      .willRespondWith({ status: 500, body: problemDetails(500, "Erro interno", "/admin/alunos") });

    await pact.executeTest(async (mock) => {
      apiClient.defaults.baseURL = mock.url;
      const err = await captureError(() => adminApi.listAlunos());
      expect(err.response?.status).toBe(500);
    });
  });
});
