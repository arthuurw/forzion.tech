import { describe, it, expect, vi, beforeEach } from "vitest";

vi.mock("./client", () => ({
  apiClient: { get: vi.fn(), post: vi.fn(), put: vi.fn(), patch: vi.fn(), delete: vi.fn() },
}));

import { apiClient } from "./client";
import { adminApi } from "./admin";

const mock = vi.mocked(apiClient);

beforeEach(() => vi.clearAllMocks());

describe("adminApi — treinadores", () => {
  it("listTreinadores GET com params", () => {
    adminApi.listTreinadores({ status: "AguardandoAprovacao" });
    expect(mock.get).toHaveBeenCalledWith("/admin/treinadores", { params: { status: "AguardandoAprovacao" } });
  });

  it("getTreinador GET", () => {
    adminApi.getTreinador("t1");
    expect(mock.get).toHaveBeenCalledWith("/admin/treinadores/t1");
  });

  it("aprovarTreinador POST com observação", () => {
    adminApi.aprovarTreinador("t1", "ok");
    expect(mock.post).toHaveBeenCalledWith("/admin/treinadores/t1/aprovar", { observacao: "ok" });
  });

  it("aprovarTreinador POST sem observação → null", () => {
    adminApi.aprovarTreinador("t1");
    expect(mock.post).toHaveBeenCalledWith("/admin/treinadores/t1/aprovar", { observacao: null });
  });

  it("reprovarTreinador POST → null default", () => {
    adminApi.reprovarTreinador("t1");
    expect(mock.post).toHaveBeenCalledWith("/admin/treinadores/t1/reprovar", { observacao: null });
  });

  it("inativarTreinador POST com observação", () => {
    adminApi.inativarTreinador("t1", "motivo");
    expect(mock.post).toHaveBeenCalledWith("/admin/treinadores/t1/inativar", { observacao: "motivo" });
  });

  it("excluirTreinador DELETE", () => {
    adminApi.excluirTreinador("t1");
    expect(mock.delete).toHaveBeenCalledWith("/admin/treinadores/t1");
  });

  it("definirCortesia PATCH com plano", () => {
    adminApi.definirCortesia("t1", "pl1");
    expect(mock.patch).toHaveBeenCalledWith("/admin/treinadores/t1/plano", { planoId: "pl1" });
  });

  it("definirCortesia PATCH com null remove a cortesia", () => {
    adminApi.definirCortesia("t1", null);
    expect(mock.patch).toHaveBeenCalledWith("/admin/treinadores/t1/plano", { planoId: null });
  });
});

describe("adminApi — planos", () => {
  it("listPlanos GET", () => {
    adminApi.listPlanos();
    expect(mock.get).toHaveBeenCalledWith("/admin/planos");
  });

  it("criarPlano POST com descrição", () => {
    adminApi.criarPlano("Pro", "Pro", 50, 199, "desc");
    expect(mock.post).toHaveBeenCalledWith("/admin/planos", {
      nome: "Pro",
      tier: "Pro",
      maxAlunos: 50,
      preco: 199,
      descricao: "desc",
    });
  });

  it("criarPlano POST sem descrição → undefined", () => {
    adminApi.criarPlano("Free", "Free", 5, 0);
    expect(mock.post).toHaveBeenCalledWith("/admin/planos", {
      nome: "Free",
      tier: "Free",
      maxAlunos: 5,
      preco: 0,
      descricao: undefined,
    });
  });

  it("atualizarPlano PATCH", () => {
    adminApi.atualizarPlano("pl1", { preco: 299 });
    expect(mock.patch).toHaveBeenCalledWith("/admin/planos/pl1", { preco: 299 });
  });

  it("excluirPlano DELETE", () => {
    adminApi.excluirPlano("pl1");
    expect(mock.delete).toHaveBeenCalledWith("/admin/planos/pl1");
  });
});

describe("adminApi — grupos musculares", () => {
  it("listGruposMusculares GET", () => {
    adminApi.listGruposMusculares();
    expect(mock.get).toHaveBeenCalledWith("/admin/grupos-musculares");
  });

  it("criarGrupoMuscular POST", () => {
    adminApi.criarGrupoMuscular("Peito");
    expect(mock.post).toHaveBeenCalledWith("/admin/grupos-musculares", { nome: "Peito" });
  });

  it("atualizarGrupoMuscular PATCH", () => {
    adminApi.atualizarGrupoMuscular("g1", "Costas");
    expect(mock.patch).toHaveBeenCalledWith("/admin/grupos-musculares/g1", { nome: "Costas" });
  });

  it("excluirGrupoMuscular DELETE", () => {
    adminApi.excluirGrupoMuscular("g1");
    expect(mock.delete).toHaveBeenCalledWith("/admin/grupos-musculares/g1");
  });
});

describe("adminApi — exercícios globais", () => {
  it("listExerciciosGlobais GET com params", () => {
    adminApi.listExerciciosGlobais({ nome: "supino" });
    expect(mock.get).toHaveBeenCalledWith("/admin/exercicios", { params: { nome: "supino" } });
  });

  it("criarExercicioGlobal POST", () => {
    const data = { nome: "Agachamento", grupoMuscularId: "g1" };
    adminApi.criarExercicioGlobal(data);
    expect(mock.post).toHaveBeenCalledWith("/admin/exercicios", data);
  });

  it("atualizarExercicioGlobal PATCH", () => {
    adminApi.atualizarExercicioGlobal("e1", { nome: "novo" });
    expect(mock.patch).toHaveBeenCalledWith("/admin/exercicios/e1", { nome: "novo" });
  });

  it("excluirExercicioGlobal DELETE", () => {
    adminApi.excluirExercicioGlobal("e1");
    expect(mock.delete).toHaveBeenCalledWith("/admin/exercicios/e1");
  });
});

describe("adminApi — alunos", () => {
  it("listAlunos GET com params", () => {
    adminApi.listAlunos({ nome: "joao" });
    expect(mock.get).toHaveBeenCalledWith("/admin/alunos", { params: { nome: "joao" } });
  });

  it("getAluno GET", () => {
    adminApi.getAluno("a1");
    expect(mock.get).toHaveBeenCalledWith("/admin/alunos/a1");
  });

  it("alterarStatusAluno PATCH", () => {
    adminApi.alterarStatusAluno("a1", "Inativo");
    expect(mock.patch).toHaveBeenCalledWith("/alunos/a1/status", { status: "Inativo" });
  });

  it("getAlunoVinculo GET", () => {
    adminApi.getAlunoVinculo("a1");
    expect(mock.get).toHaveBeenCalledWith("/admin/alunos/a1/vinculo");
  });

  it("getAlunoFichas GET com params", () => {
    adminApi.getAlunoFichas("a1", { pagina: 1 });
    expect(mock.get).toHaveBeenCalledWith("/admin/alunos/a1/fichas", { params: { pagina: 1 } });
  });

  it("getFichaDetalhe GET", () => {
    adminApi.getFichaDetalhe("ta1");
    expect(mock.get).toHaveBeenCalledWith("/admin/fichas/ta1");
  });

  it("getAlunoExecucoes GET com params", () => {
    adminApi.getAlunoExecucoes("a1", { pagina: 2 });
    expect(mock.get).toHaveBeenCalledWith("/admin/alunos/a1/execucoes", { params: { pagina: 2 } });
  });

  it("getAlunoProgressao GET com params", () => {
    adminApi.getAlunoProgressao("a1", { de: "2026-01-01" });
    expect(mock.get).toHaveBeenCalledWith("/admin/alunos/a1/progressao", { params: { de: "2026-01-01" } });
  });
});

describe("adminApi — sub-recursos treinador", () => {
  it("getTreinadorAlunos GET com params", () => {
    adminApi.getTreinadorAlunos("t1", { status: "Ativo" });
    expect(mock.get).toHaveBeenCalledWith("/admin/treinadores/t1/alunos", { params: { status: "Ativo" } });
  });

  it("getTreinadorVinculos GET com params", () => {
    adminApi.getTreinadorVinculos("t1", { status: "Ativo" });
    expect(mock.get).toHaveBeenCalledWith("/admin/treinadores/t1/vinculos", { params: { status: "Ativo" } });
  });

  it("getTreinadorTreinos GET com params", () => {
    adminApi.getTreinadorTreinos("t1", { nome: "x" });
    expect(mock.get).toHaveBeenCalledWith("/admin/treinadores/t1/treinos", { params: { nome: "x" } });
  });

  it("getTreino GET", () => {
    adminApi.getTreino("t1");
    expect(mock.get).toHaveBeenCalledWith("/admin/treinos/t1");
  });

  it("getTreinadorPacotes GET", () => {
    adminApi.getTreinadorPacotes("t1");
    expect(mock.get).toHaveBeenCalledWith("/admin/treinadores/t1/pacotes");
  });
});

describe("adminApi — stats + LGPD", () => {
  it("getDashboardStats GET", () => {
    adminApi.getDashboardStats();
    expect(mock.get).toHaveBeenCalledWith("/admin/stats/dashboard");
  });

  it("exportarDadosConta GET com responseType blob", () => {
    adminApi.exportarDadosConta("c1");
    expect(mock.get).toHaveBeenCalledWith("/admin/contas/c1/lgpd/exportar", { responseType: "blob" });
  });

  it("anonimizarConta DELETE", () => {
    adminApi.anonimizarConta("c1");
    expect(mock.delete).toHaveBeenCalledWith("/admin/contas/c1/lgpd");
  });
});
