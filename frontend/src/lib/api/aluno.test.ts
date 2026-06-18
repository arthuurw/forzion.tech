import { describe, it, expect, vi, beforeEach } from "vitest";

vi.mock("./client", () => ({
  apiClient: { get: vi.fn(), post: vi.fn() },
}));

import { apiClient } from "./client";
import { alunoApi } from "./aluno";

const mock = vi.mocked(apiClient);

beforeEach(() => vi.clearAllMocks());

describe("alunoApi", () => {
  it("listFichas GET com params", () => {
    alunoApi.listFichas({ pagina: 1, tamanhoPagina: 20 });
    expect(mock.get).toHaveBeenCalledWith("/aluno/fichas", { params: { pagina: 1, tamanhoPagina: 20 } });
  });

  it("listFichas GET sem params", () => {
    alunoApi.listFichas();
    expect(mock.get).toHaveBeenCalledWith("/aluno/fichas", { params: undefined });
  });

  it("getFicha GET por id", () => {
    alunoApi.getFicha("ta1");
    expect(mock.get).toHaveBeenCalledWith("/aluno/fichas/ta1");
  });

  it("listExecucoes GET com params", () => {
    alunoApi.listExecucoes({ pagina: 3 });
    expect(mock.get).toHaveBeenCalledWith("/aluno/execucoes", { params: { pagina: 3 } });
  });

  it("criarExecucao POST", () => {
    const data = {
      treinoId: "t1",
      dataExecucao: "2026-06-01",
      exercicios: [{ treinoExercicioId: "te1", seriesExecutadas: 3, repeticoesExecutadas: 10 }],
    };
    alunoApi.criarExecucao(data);
    expect(mock.post).toHaveBeenCalledWith("/aluno/execucoes", data);
  });

  it("criarExecucao POST com idempotencyKey envia header Idempotency-Key", () => {
    const data = {
      treinoId: "t1",
      dataExecucao: "2026-06-01",
      exercicios: [{ treinoExercicioId: "te1", seriesExecutadas: 3, repeticoesExecutadas: 10 }],
    };
    alunoApi.criarExecucao(data, { idempotencyKey: "11111111-1111-1111-1111-111111111111" });
    expect(mock.post).toHaveBeenCalledWith("/aluno/execucoes", data, {
      headers: { "Idempotency-Key": "11111111-1111-1111-1111-111111111111" },
    });
  });

  it("getMinhaProgressao GET com de/ate", () => {
    alunoApi.getMinhaProgressao("2026-01-01", "2026-02-01");
    expect(mock.get).toHaveBeenCalledWith("/aluno/progressao", {
      params: { de: "2026-01-01", ate: "2026-02-01" },
    });
  });

  it("getMinhaProgressao GET sem args → undefined params", () => {
    alunoApi.getMinhaProgressao();
    expect(mock.get).toHaveBeenCalledWith("/aluno/progressao", { params: { de: undefined, ate: undefined } });
  });

  it("getMeuVinculo GET", () => {
    alunoApi.getMeuVinculo();
    expect(mock.get).toHaveBeenCalledWith("/aluno/vinculo");
  });

  it("solicitarTrocaTreinador POST", () => {
    alunoApi.solicitarTrocaTreinador("t2", "p1");
    expect(mock.post).toHaveBeenCalledWith("/aluno/troca-treinador", { novoTreinadorId: "t2", pacoteId: "p1" });
  });
});
