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
