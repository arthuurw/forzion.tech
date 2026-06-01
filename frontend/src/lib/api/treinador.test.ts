import { describe, it, expect, vi, beforeEach } from "vitest";

vi.mock("./client", () => ({
  apiClient: { get: vi.fn(), post: vi.fn(), put: vi.fn(), patch: vi.fn(), delete: vi.fn() },
}));

import { apiClient } from "./client";
import { treinadorApi } from "./treinador";

const mock = vi.mocked(apiClient);

beforeEach(() => vi.clearAllMocks());

describe("treinadorApi — alunos", () => {
  it("listAlunos GET com params", () => {
    treinadorApi.listAlunos({ status: "Ativo", pagina: 2 });
    expect(mock.get).toHaveBeenCalledWith("/treinador/alunos", { params: { status: "Ativo", pagina: 2 } });
  });

  it("listAlunos GET sem params", () => {
    treinadorApi.listAlunos();
    expect(mock.get).toHaveBeenCalledWith("/treinador/alunos", { params: undefined });
  });

  it("getAluno GET por id", () => {
    treinadorApi.getAluno("a1");
    expect(mock.get).toHaveBeenCalledWith("/treinador/alunos/a1");
  });

  it("getFichasDoAluno GET", () => {
    treinadorApi.getFichasDoAluno("a1");
    expect(mock.get).toHaveBeenCalledWith("/treinador/alunos/a1/fichas");
  });

  it("getProgressaoAluno GET com params", () => {
    treinadorApi.getProgressaoAluno("a1", { de: "2026-01-01", ate: "2026-02-01" });
    expect(mock.get).toHaveBeenCalledWith("/treinador/alunos/a1/progressao", {
      params: { de: "2026-01-01", ate: "2026-02-01" },
    });
  });

  it("reativarAluno POST com pacoteId", () => {
    treinadorApi.reativarAluno("a1", "p1");
    expect(mock.post).toHaveBeenCalledWith("/treinador/alunos/a1/reativar", { pacoteId: "p1" });
  });
});

describe("treinadorApi — vínculos", () => {
  it("listVinculos GET com params", () => {
    treinadorApi.listVinculos({ status: "AguardandoAprovacao" });
    expect(mock.get).toHaveBeenCalledWith("/treinador/vinculos", { params: { status: "AguardandoAprovacao" } });
  });

  it("aprovarVinculo POST default trarFichas=false", () => {
    treinadorApi.aprovarVinculo("v1", "p1");
    expect(mock.post).toHaveBeenCalledWith("/treinador/vinculos/v1/aprovar", { pacoteId: "p1", trarFichas: false });
  });

  it("aprovarVinculo POST trarFichas=true", () => {
    treinadorApi.aprovarVinculo("v1", "p1", true);
    expect(mock.post).toHaveBeenCalledWith("/treinador/vinculos/v1/aprovar", { pacoteId: "p1", trarFichas: true });
  });

  it("desvincularAluno POST com observação", () => {
    treinadorApi.desvincularAluno("v1", "motivo");
    expect(mock.post).toHaveBeenCalledWith("/treinador/vinculos/v1/desvincular", { observacao: "motivo" });
  });

  it("desvincularAluno POST sem observação → null", () => {
    treinadorApi.desvincularAluno("v1");
    expect(mock.post).toHaveBeenCalledWith("/treinador/vinculos/v1/desvincular", { observacao: null });
  });
});

describe("treinadorApi — fichas/treinos", () => {
  it("listFichas GET com params", () => {
    treinadorApi.listFichas({ nome: "x" });
    expect(mock.get).toHaveBeenCalledWith("/treinador/treinos", { params: { nome: "x" } });
  });

  it("getFicha GET", () => {
    treinadorApi.getFicha("t1");
    expect(mock.get).toHaveBeenCalledWith("/treinos/t1");
  });

  it("criarFicha POST", () => {
    const data = { nome: "Ficha", objetivo: "Hipertrofia" as const };
    treinadorApi.criarFicha(data);
    expect(mock.post).toHaveBeenCalledWith("/treinos", data);
  });

  it("atualizarFicha PATCH", () => {
    treinadorApi.atualizarFicha("t1", { nome: "novo" });
    expect(mock.patch).toHaveBeenCalledWith("/treinos/t1", { nome: "novo" });
  });

  it("excluirFicha DELETE", () => {
    treinadorApi.excluirFicha("t1");
    expect(mock.delete).toHaveBeenCalledWith("/treinos/t1");
  });

  it("duplicarFicha POST", () => {
    treinadorApi.duplicarFicha("t1");
    expect(mock.post).toHaveBeenCalledWith("/treinos/t1/duplicar");
  });

  it("vincularFichaAoAluno POST", () => {
    treinadorApi.vincularFichaAoAluno("a1", "t1");
    expect(mock.post).toHaveBeenCalledWith("/treinador/alunos/a1/fichas/t1");
  });

  it("listAlunosVinculados GET", () => {
    treinadorApi.listAlunosVinculados("t1");
    expect(mock.get).toHaveBeenCalledWith("/treinos/t1/alunos");
  });
});

describe("treinadorApi — exercícios de treino", () => {
  it("adicionarExercicio POST", () => {
    const data = { exercicioId: "e1", series: [{ quantidade: 3, repeticoesMin: 8 }] };
    treinadorApi.adicionarExercicio("t1", data);
    expect(mock.post).toHaveBeenCalledWith("/treinos/t1/exercicios", data);
  });

  it("removerExercicio DELETE", () => {
    treinadorApi.removerExercicio("t1", "e1");
    expect(mock.delete).toHaveBeenCalledWith("/treinos/t1/exercicios/e1");
  });

  it("editarExercicioTreino PUT", () => {
    const data = { series: [{ quantidade: 4, repeticoesMin: 10 }] };
    treinadorApi.editarExercicioTreino("t1", "te1", data);
    expect(mock.put).toHaveBeenCalledWith("/treinos/t1/exercicios/te1", data);
  });

  it("atualizarObservacaoExercicio PATCH com observação", () => {
    treinadorApi.atualizarObservacaoExercicio("t1", "te1", "obs");
    expect(mock.patch).toHaveBeenCalledWith("/treinos/t1/exercicios/te1/observacao", { observacao: "obs" });
  });

  it("atualizarObservacaoExercicio PATCH com null", () => {
    treinadorApi.atualizarObservacaoExercicio("t1", "te1", null);
    expect(mock.patch).toHaveBeenCalledWith("/treinos/t1/exercicios/te1/observacao", { observacao: null });
  });
});

describe("treinadorApi — exercícios/grupos", () => {
  it("listGruposMusculares GET", () => {
    treinadorApi.listGruposMusculares();
    expect(mock.get).toHaveBeenCalledWith("/treinador/grupos-musculares");
  });

  it("listExercicios GET com params", () => {
    treinadorApi.listExercicios({ global: true });
    expect(mock.get).toHaveBeenCalledWith("/treinador/exercicios", { params: { global: true } });
  });

  it("criarExercicio POST", () => {
    const data = { nome: "Supino", grupoMuscularId: "g1" };
    treinadorApi.criarExercicio(data);
    expect(mock.post).toHaveBeenCalledWith("/treinador/exercicios", data);
  });

  it("copiarExercicioGlobal POST", () => {
    treinadorApi.copiarExercicioGlobal("e1");
    expect(mock.post).toHaveBeenCalledWith("/treinador/exercicios/e1/copiar");
  });

  it("atualizarExercicio PATCH", () => {
    treinadorApi.atualizarExercicio("e1", { nome: "novo" });
    expect(mock.patch).toHaveBeenCalledWith("/treinador/exercicios/e1", { nome: "novo" });
  });

  it("excluirExercicio DELETE", () => {
    treinadorApi.excluirExercicio("e1");
    expect(mock.delete).toHaveBeenCalledWith("/treinador/exercicios/e1");
  });
});

describe("treinadorApi — pacotes", () => {
  it("listPacotes GET", () => {
    treinadorApi.listPacotes();
    expect(mock.get).toHaveBeenCalledWith("/treinador/pacotes");
  });

  it("criarPacote POST", () => {
    const data = { nome: "Mensal", preco: 100 };
    treinadorApi.criarPacote(data);
    expect(mock.post).toHaveBeenCalledWith("/treinador/pacotes", data);
  });

  it("atualizarPacote PATCH", () => {
    treinadorApi.atualizarPacote("p1", { preco: 120 });
    expect(mock.patch).toHaveBeenCalledWith("/treinador/pacotes/p1", { preco: 120 });
  });

  it("excluirPacote DELETE", () => {
    treinadorApi.excluirPacote("p1");
    expect(mock.delete).toHaveBeenCalledWith("/treinador/pacotes/p1");
  });
});
