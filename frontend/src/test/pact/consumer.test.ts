/**
 * Contratos consumer-driven (Pact) — Fase 15.
 *
 * Exercita o `apiClient` REAL (e os modulos de API que o usam) contra o mock
 * server do Pact, re-apontando `apiClient.defaults.baseURL` para a URL efemera
 * do mock. Cada `executeTest` que passa grava/mescla uma interacao no pact
 * `pacts/forzion-frontend-forzion-api.json`.
 *
 * Roda isolado dos projects do Vitest (env node, sem MSW) via
 * `vitest.pact.config.mts` — disparado por `npm run test:contract`.
 *
 * Os matchers (MatchersV3) garantem que o contrato valida ESTRUTURA/TIPO, nao
 * valores literais. So declaramos os campos que o consumer realmente consome;
 * campos extras retornados pelo provider sao aceitos na verificacao.
 */
import { afterEach, describe, expect, it } from "vitest";
import { PactV3, MatchersV3 } from "@pact-foundation/pact";
import { apiClient } from "@/lib/api/client";
import { alunoApi } from "@/lib/api/aluno";
import { contaApi } from "@/lib/api/conta";
import { adminApi } from "@/lib/api/admin";
import { CONSUMER, PROVIDER, PACT_DIR } from "./support/pact-config";

const { like, eachLike, string, integer, uuid, nullValue } = MatchersV3;

const EXAMPLE_UUID = "11111111-1111-1111-1111-111111111111";

const originalBaseURL = apiClient.defaults.baseURL;
afterEach(() => {
  apiClient.defaults.baseURL = originalBaseURL;
});

function newPact() {
  return new PactV3({ consumer: CONSUMER, provider: PROVIDER, dir: PACT_DIR, logLevel: "warn" });
}

describe("Contrato: forzion-frontend -> forzion-api", () => {
  it("GET /aluno/fichas retorna pagina de fichas", async () => {
    const pact = newPact()
      .given("aluno autenticado possui fichas de treino")
      .uponReceiving("listagem de fichas do aluno")
      .withRequest({ method: "GET", path: "/aluno/fichas" })
      .willRespondWith({
        status: 200,
        body: like({
          items: eachLike({
            treinoAlunoId: uuid(EXAMPLE_UUID),
            treinoId: uuid(EXAMPLE_UUID),
            nomeTreino: string("Treino A - Superiores"),
            objetivo: string("Hipertrofia"),
            status: string("Ativo"),
          }),
          total: integer(1),
          pagina: integer(1),
          tamanhoPagina: integer(20),
        }),
      });

    await pact.executeTest(async (mock) => {
      apiClient.defaults.baseURL = mock.url;
      const res = await alunoApi.listFichas();
      expect(res.status).toBe(200);
      expect(res.data.items.length).toBeGreaterThan(0);
      expect(res.data.items[0].treinoAlunoId).toBeTruthy();
      expect(res.data.tamanhoPagina).toBe(20);
    });
  });

  it("GET /aluno/vinculo retorna vinculo ativo e pendente nulo", async () => {
    const pact = newPact()
      .given("aluno autenticado possui vinculo ativo e nenhum pendente")
      .uponReceiving("consulta do vinculo do aluno")
      .withRequest({ method: "GET", path: "/aluno/vinculo" })
      .willRespondWith({
        status: 200,
        body: like({
          vinculoAtivo: like({
            vinculoId: uuid(EXAMPLE_UUID),
            treinadorId: uuid(EXAMPLE_UUID),
            nomeTreinador: string("Coach Silva"),
            status: string("Ativo"),
            dataInicio: string("2026-01-10"),
            createdAt: string("2026-01-01T12:00:00Z"),
          }),
          vinculoPendente: nullValue(),
        }),
      });

    await pact.executeTest(async (mock) => {
      apiClient.defaults.baseURL = mock.url;
      const res = await alunoApi.getMeuVinculo();
      expect(res.status).toBe(200);
      expect(res.data.vinculoAtivo?.nomeTreinador).toBeTruthy();
      expect(res.data.vinculoPendente).toBeNull();
    });
  });

  it("GET /conta/perfil retorna perfil da conta", async () => {
    const pact = newPact()
      .given("conta autenticada existe")
      .uponReceiving("consulta do perfil da conta")
      .withRequest({ method: "GET", path: "/conta/perfil" })
      .willRespondWith({
        status: 200,
        body: like({
          nome: string("Arthur Webster"),
          email: string("arthur@forzion.tech"),
          tipoConta: string("Aluno"),
        }),
      });

    await pact.executeTest(async (mock) => {
      apiClient.defaults.baseURL = mock.url;
      const res = await contaApi.getPerfil();
      expect(res.status).toBe(200);
      expect(res.data.email).toBeTruthy();
      expect(res.data.tipoConta).toBeTruthy();
    });
  });

  it("GET /admin/alunos retorna pagina de alunos", async () => {
    const pact = newPact()
      .given("admin autenticado e existem alunos cadastrados")
      .uponReceiving("listagem de alunos do admin")
      .withRequest({ method: "GET", path: "/admin/alunos" })
      .willRespondWith({
        status: 200,
        body: like({
          items: eachLike({
            alunoId: uuid(EXAMPLE_UUID),
            nome: string("Joao Silva"),
            email: string("joao@exemplo.com"),
            status: string("Ativo"),
            contaId: uuid(EXAMPLE_UUID),
            createdAt: string("2026-01-01T12:00:00Z"),
          }),
          total: integer(1),
          pagina: integer(1),
          tamanhoPagina: integer(20),
        }),
      });

    await pact.executeTest(async (mock) => {
      apiClient.defaults.baseURL = mock.url;
      const res = await adminApi.listAlunos();
      expect(res.status).toBe(200);
      expect(res.data.items.length).toBeGreaterThan(0);
      expect(res.data.items[0].nome).toBeTruthy();
    });
  });
});
