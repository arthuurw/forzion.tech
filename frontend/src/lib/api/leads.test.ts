import { describe, it, expect, vi, beforeEach } from "vitest";

vi.mock("./client", () => ({
  apiClient: { get: vi.fn(), post: vi.fn(), put: vi.fn(), patch: vi.fn(), delete: vi.fn() },
}));

import { apiClient } from "./client";
import { leadsApi } from "./leads";

const mock = vi.mocked(apiClient);

beforeEach(() => vi.clearAllMocks());

describe("leadsApi — listar", () => {
  it("GET com filtros combinados", () => {
    leadsApi.listar({ pagina: 2, tamanhoPagina: 20, status: "Novo", origem: "Agent", termo: "maria" });
    expect(mock.get).toHaveBeenCalledWith("/treinador/leads", {
      params: { pagina: 2, tamanhoPagina: 20, status: "Novo", origem: "Agent", termo: "maria" },
    });
  });

  it("propaga erro do backend", async () => {
    mock.get.mockRejectedValueOnce({ response: { status: 500 } });
    await expect(leadsApi.listar({ pagina: 1, tamanhoPagina: 20 })).rejects.toBeDefined();
  });
});

describe("leadsApi — obter", () => {
  it("GET por id", () => {
    leadsApi.obter("lead-1");
    expect(mock.get).toHaveBeenCalledWith("/treinador/leads/lead-1");
  });

  it("propaga 404", async () => {
    mock.get.mockRejectedValueOnce({ response: { status: 404 } });
    await expect(leadsApi.obter("inexistente")).rejects.toBeDefined();
  });
});

describe("leadsApi — criarManual", () => {
  it("POST com payload", () => {
    leadsApi.criarManual({
      nome: "Maria",
      contatoTipo: "Email",
      contatoValor: "maria@example.com",
      interesse: "musculação",
      consentimentoFinalidade: "Contato comercial",
    });
    expect(mock.post).toHaveBeenCalledWith("/treinador/leads", {
      nome: "Maria",
      contatoTipo: "Email",
      contatoValor: "maria@example.com",
      interesse: "musculação",
      consentimentoFinalidade: "Contato comercial",
    });
  });

  it("propaga erro de validação", async () => {
    mock.post.mockRejectedValueOnce({ response: { status: 400 } });
    await expect(
      leadsApi.criarManual({ nome: "", contatoTipo: "Email", contatoValor: "", consentimentoFinalidade: "" }),
    ).rejects.toBeDefined();
  });
});

describe("leadsApi — atualizarStatus", () => {
  it("PATCH com motivo de descarte", () => {
    leadsApi.atualizarStatus("lead-1", { novoStatus: "Descartado", motivo: "SemInteresse", observacao: "não respondeu" });
    expect(mock.patch).toHaveBeenCalledWith("/treinador/leads/lead-1/status", {
      novoStatus: "Descartado",
      motivo: "SemInteresse",
      observacao: "não respondeu",
    });
  });

  it("propaga conflito de estado terminal", async () => {
    mock.patch.mockRejectedValueOnce({ response: { status: 422 } });
    await expect(leadsApi.atualizarStatus("lead-1", { novoStatus: "EmContato" })).rejects.toBeDefined();
  });
});

describe("leadsApi — registrarInteracao", () => {
  it("POST com observação", () => {
    leadsApi.registrarInteracao("lead-1", "Liguei, sem resposta.");
    expect(mock.post).toHaveBeenCalledWith("/treinador/leads/lead-1/interacoes", { observacao: "Liguei, sem resposta." });
  });

  it("propaga erro", async () => {
    mock.post.mockRejectedValueOnce({ response: { status: 404 } });
    await expect(leadsApi.registrarInteracao("outro-treinador", "x")).rejects.toBeDefined();
  });
});

describe("leadsApi — enviarConvite", () => {
  it("POST sem corpo", () => {
    leadsApi.enviarConvite("lead-1");
    expect(mock.post).toHaveBeenCalledWith("/treinador/leads/lead-1/convite", {});
  });

  it("propaga recusa (telefone)", async () => {
    mock.post.mockRejectedValueOnce({ response: { status: 422 } });
    await expect(leadsApi.enviarConvite("lead-1")).rejects.toBeDefined();
  });
});

describe("leadsApi — metricas", () => {
  it("GET com período", () => {
    leadsApi.metricas("2026-07-01", "2026-08-01");
    expect(mock.get).toHaveBeenCalledWith("/treinador/leads/metricas", {
      params: { inicio: "2026-07-01", fim: "2026-08-01" },
    });
  });

  it("GET sem período", () => {
    leadsApi.metricas();
    expect(mock.get).toHaveBeenCalledWith("/treinador/leads/metricas", { params: { inicio: undefined, fim: undefined } });
  });

  it("propaga erro", async () => {
    mock.get.mockRejectedValueOnce({ response: { status: 500 } });
    await expect(leadsApi.metricas()).rejects.toBeDefined();
  });
});
