import { describe, it, expect, vi, beforeEach } from "vitest";

vi.mock("./client", () => ({
  apiClient: { get: vi.fn(), post: vi.fn(), put: vi.fn(), patch: vi.fn(), delete: vi.fn() },
}));

import { apiClient } from "./client";
import { agendaApi } from "./agenda";

const mock = vi.mocked(apiClient);

beforeEach(() => vi.clearAllMocks());

describe("agendaApi — listarBloqueios", () => {
  it("GET sem parâmetros", () => {
    agendaApi.listarBloqueios();
    expect(mock.get).toHaveBeenCalledWith("/treinador/agenda/bloqueios");
  });

  it("propaga erro do backend", async () => {
    mock.get.mockRejectedValueOnce({ response: { status: 500 } });
    await expect(agendaApi.listarBloqueios()).rejects.toBeDefined();
  });
});

describe("agendaApi — criarBloqueio", () => {
  it("POST com payload pontual", () => {
    agendaApi.criarBloqueio({
      tipo: "Pontual",
      inicioUtc: "2026-09-01T10:00:00.000Z",
      fimUtc: "2026-09-01T11:00:00.000Z",
      motivo: "Viagem",
    });
    expect(mock.post).toHaveBeenCalledWith("/treinador/agenda/bloqueios", {
      tipo: "Pontual",
      inicioUtc: "2026-09-01T10:00:00.000Z",
      fimUtc: "2026-09-01T11:00:00.000Z",
      motivo: "Viagem",
    });
  });

  it("POST com payload recorrente", () => {
    agendaApi.criarBloqueio({
      tipo: "RecorrenteSemanal",
      diaSemana: 1,
      horaInicio: "12:00",
      horaFim: "13:00",
    });
    expect(mock.post).toHaveBeenCalledWith("/treinador/agenda/bloqueios", {
      tipo: "RecorrenteSemanal",
      diaSemana: 1,
      horaInicio: "12:00",
      horaFim: "13:00",
    });
  });

  it("propaga erro de validação", async () => {
    mock.post.mockRejectedValueOnce({ response: { status: 400 } });
    await expect(
      agendaApi.criarBloqueio({ tipo: "Pontual", inicioUtc: "x", fimUtc: "x" }),
    ).rejects.toBeDefined();
  });
});

describe("agendaApi — apagarBloqueio", () => {
  it("DELETE por id", () => {
    agendaApi.apagarBloqueio("bloqueio-1");
    expect(mock.delete).toHaveBeenCalledWith("/treinador/agenda/bloqueios/bloqueio-1");
  });

  it("propaga 404 cross-tenant", async () => {
    mock.delete.mockRejectedValueOnce({ response: { status: 404 } });
    await expect(agendaApi.apagarBloqueio("de-outro-treinador")).rejects.toBeDefined();
  });
});

describe("agendaApi — obterPolitica", () => {
  it("GET sem parâmetros", () => {
    agendaApi.obterPolitica();
    expect(mock.get).toHaveBeenCalledWith("/treinador/agenda/politica");
  });

  it("propaga erro", async () => {
    mock.get.mockRejectedValueOnce({ response: { status: 500 } });
    await expect(agendaApi.obterPolitica()).rejects.toBeDefined();
  });
});

describe("agendaApi — atualizarPolitica", () => {
  it("PUT com payload", () => {
    agendaApi.atualizarPolitica({ antecedenciaMinimaHoras: 4, horizonteDias: 30 });
    expect(mock.put).toHaveBeenCalledWith("/treinador/agenda/politica", {
      antecedenciaMinimaHoras: 4,
      horizonteDias: 30,
    });
  });

  it("propaga erro de validação", async () => {
    mock.put.mockRejectedValueOnce({ response: { status: 400 } });
    await expect(agendaApi.atualizarPolitica({ antecedenciaMinimaHoras: -1, horizonteDias: 0 })).rejects.toBeDefined();
  });
});

describe("agendaApi — listarSolicitacoes", () => {
  it("GET sem parâmetros usa objeto vazio", () => {
    agendaApi.listarSolicitacoes();
    expect(mock.get).toHaveBeenCalledWith("/treinador/agenda/solicitacoes", { params: {} });
  });

  it("GET com filtro de status e paginação", () => {
    agendaApi.listarSolicitacoes({ status: "PendenteAgente", pagina: 2, tamanhoPagina: 10 });
    expect(mock.get).toHaveBeenCalledWith("/treinador/agenda/solicitacoes", {
      params: { status: "PendenteAgente", pagina: 2, tamanhoPagina: 10 },
    });
  });

  it("propaga erro do backend", async () => {
    mock.get.mockRejectedValueOnce({ response: { status: 500 } });
    await expect(agendaApi.listarSolicitacoes()).rejects.toBeDefined();
  });
});

describe("agendaApi — confirmarSolicitacao", () => {
  it("POST por id", () => {
    agendaApi.confirmarSolicitacao("solicitacao-1");
    expect(mock.post).toHaveBeenCalledWith("/treinador/agenda/solicitacoes/solicitacao-1/confirmar", {});
  });

  it("propaga conflito de capacidade", async () => {
    mock.post.mockRejectedValueOnce({ response: { status: 409 } });
    await expect(agendaApi.confirmarSolicitacao("solicitacao-1")).rejects.toBeDefined();
  });
});

describe("agendaApi — recusarSolicitacao", () => {
  it("POST com motivo", () => {
    agendaApi.recusarSolicitacao("solicitacao-1", "Fora do horário disponível");
    expect(mock.post).toHaveBeenCalledWith("/treinador/agenda/solicitacoes/solicitacao-1/recusar", {
      motivo: "Fora do horário disponível",
    });
  });

  it("POST sem motivo envia null", () => {
    agendaApi.recusarSolicitacao("solicitacao-1");
    expect(mock.post).toHaveBeenCalledWith("/treinador/agenda/solicitacoes/solicitacao-1/recusar", { motivo: null });
  });

  it("propaga erro de transição inválida", async () => {
    mock.post.mockRejectedValueOnce({ response: { status: 422 } });
    await expect(agendaApi.recusarSolicitacao("solicitacao-1")).rejects.toBeDefined();
  });
});

describe("agendaApi — cancelarSolicitacao", () => {
  it("POST com motivo", () => {
    agendaApi.cancelarSolicitacao("solicitacao-1", "Aluno desistiu");
    expect(mock.post).toHaveBeenCalledWith("/treinador/agenda/solicitacoes/solicitacao-1/cancelar", {
      motivo: "Aluno desistiu",
    });
  });

  it("propaga 404 cross-tenant", async () => {
    mock.post.mockRejectedValueOnce({ response: { status: 404 } });
    await expect(agendaApi.cancelarSolicitacao("de-outro-treinador")).rejects.toBeDefined();
  });
});
