import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("@/lib/api/client", () => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn(),
    patch: vi.fn(),
    delete: vi.fn(),
  },
}));

import { apiClient } from "@/lib/api/client";
import { adminApi } from "@/lib/api/admin";

const mockGet    = vi.mocked(apiClient.get);
const mockPost   = vi.mocked(apiClient.post);
const mockPatch  = vi.mocked(apiClient.patch);
const mockDelete = vi.mocked(apiClient.delete);

beforeEach(() => {
  vi.clearAllMocks();
  mockGet.mockResolvedValue({ data: {} });
  mockPost.mockResolvedValue({ data: {} });
  mockPatch.mockResolvedValue({ data: {} });
  mockDelete.mockResolvedValue({ data: {} });
});

// ─── Alunos ─────────────────────────────────────────────────────────────────

describe("adminApi.listAlunos", () => {
  it("chama GET /admin/alunos sem params", () => {
    adminApi.listAlunos();
    expect(mockGet).toHaveBeenCalledWith("/admin/alunos", { params: undefined });
  });

  it("chama GET /admin/alunos com nome e status", () => {
    adminApi.listAlunos({ nome: "João", status: "Ativo", pagina: 1, tamanhoPagina: 20 });
    expect(mockGet).toHaveBeenCalledWith("/admin/alunos", {
      params: { nome: "João", status: "Ativo", pagina: 1, tamanhoPagina: 20 },
    });
  });

  it("retorna resultado do apiClient", async () => {
    const data = { items: [], total: 0, pagina: 1, tamanhoPagina: 20 };
    mockGet.mockResolvedValueOnce({ data });
    const res = await adminApi.listAlunos();
    expect(res.data).toEqual(data);
  });
});

describe("adminApi.getAluno", () => {
  it("chama GET /admin/alunos/{id}", () => {
    adminApi.getAluno("abc-123");
    expect(mockGet).toHaveBeenCalledWith("/admin/alunos/abc-123");
  });
});

describe("adminApi.getAlunoVinculo", () => {
  it("chama GET /admin/alunos/{id}/vinculo", () => {
    adminApi.getAlunoVinculo("abc-123");
    expect(mockGet).toHaveBeenCalledWith("/admin/alunos/abc-123/vinculo");
  });
});

describe("adminApi.getAlunoFichas", () => {
  it("chama GET /admin/alunos/{id}/fichas sem params", () => {
    adminApi.getAlunoFichas("abc-123");
    expect(mockGet).toHaveBeenCalledWith("/admin/alunos/abc-123/fichas", { params: undefined });
  });

  it("chama GET /admin/alunos/{id}/fichas com paginação", () => {
    adminApi.getAlunoFichas("abc-123", { pagina: 2, tamanhoPagina: 10 });
    expect(mockGet).toHaveBeenCalledWith("/admin/alunos/abc-123/fichas", {
      params: { pagina: 2, tamanhoPagina: 10 },
    });
  });
});

describe("adminApi.getFichaDetalhe", () => {
  it("chama GET /admin/fichas/{treinoAlunoId}", () => {
    adminApi.getFichaDetalhe("ficha-456");
    expect(mockGet).toHaveBeenCalledWith("/admin/fichas/ficha-456");
  });
});

describe("adminApi.getAlunoExecucoes", () => {
  it("chama GET /admin/alunos/{id}/execucoes sem params", () => {
    adminApi.getAlunoExecucoes("abc-123");
    expect(mockGet).toHaveBeenCalledWith("/admin/alunos/abc-123/execucoes", { params: undefined });
  });

  it("chama GET /admin/alunos/{id}/execucoes com paginação", () => {
    adminApi.getAlunoExecucoes("abc-123", { pagina: 1, tamanhoPagina: 50 });
    expect(mockGet).toHaveBeenCalledWith("/admin/alunos/abc-123/execucoes", {
      params: { pagina: 1, tamanhoPagina: 50 },
    });
  });
});

describe("adminApi.getAlunoProgressao", () => {
  it("chama GET /admin/alunos/{id}/progressao sem params", () => {
    adminApi.getAlunoProgressao("abc-123");
    expect(mockGet).toHaveBeenCalledWith("/admin/alunos/abc-123/progressao", { params: undefined });
  });

  it("chama GET /admin/alunos/{id}/progressao com período", () => {
    adminApi.getAlunoProgressao("abc-123", { de: "2025-01-01", ate: "2025-03-31" });
    expect(mockGet).toHaveBeenCalledWith("/admin/alunos/abc-123/progressao", {
      params: { de: "2025-01-01", ate: "2025-03-31" },
    });
  });
});

// ─── Sub-recursos de treinadores ────────────────────────────────────────────

describe("adminApi.getTreinadorAlunos", () => {
  it("chama GET /admin/treinadores/{id}/alunos sem params", () => {
    adminApi.getTreinadorAlunos("t-789");
    expect(mockGet).toHaveBeenCalledWith("/admin/treinadores/t-789/alunos", { params: undefined });
  });

  it("chama GET /admin/treinadores/{id}/alunos com status", () => {
    adminApi.getTreinadorAlunos("t-789", { status: "Ativo", pagina: 1, tamanhoPagina: 20 });
    expect(mockGet).toHaveBeenCalledWith("/admin/treinadores/t-789/alunos", {
      params: { status: "Ativo", pagina: 1, tamanhoPagina: 20 },
    });
  });
});

describe("adminApi.getTreinadorVinculos", () => {
  it("chama GET /admin/treinadores/{id}/vinculos sem params", () => {
    adminApi.getTreinadorVinculos("t-789");
    expect(mockGet).toHaveBeenCalledWith("/admin/treinadores/t-789/vinculos", { params: undefined });
  });

  it("chama GET /admin/treinadores/{id}/vinculos com status", () => {
    adminApi.getTreinadorVinculos("t-789", { status: "AguardandoAprovacao" });
    expect(mockGet).toHaveBeenCalledWith("/admin/treinadores/t-789/vinculos", {
      params: { status: "AguardandoAprovacao" },
    });
  });
});

describe("adminApi.getTreinadorTreinos", () => {
  it("chama GET /admin/treinadores/{id}/treinos sem params", () => {
    adminApi.getTreinadorTreinos("t-789");
    expect(mockGet).toHaveBeenCalledWith("/admin/treinadores/t-789/treinos", { params: undefined });
  });

  it("chama GET /admin/treinadores/{id}/treinos com nome e objetivo", () => {
    adminApi.getTreinadorTreinos("t-789", { nome: "Costas", objetivo: "Hipertrofia" });
    expect(mockGet).toHaveBeenCalledWith("/admin/treinadores/t-789/treinos", {
      params: { nome: "Costas", objetivo: "Hipertrofia" },
    });
  });
});

describe("adminApi.getTreino", () => {
  it("chama GET /admin/treinos/{id}", () => {
    adminApi.getTreino("treino-001");
    expect(mockGet).toHaveBeenCalledWith("/admin/treinos/treino-001");
  });

  it("retorna TreinoResponse do apiClient", async () => {
    const data = { treinoId: "treino-001", nome: "Treino A", objetivo: "Hipertrofia" };
    mockGet.mockResolvedValueOnce({ data });
    const res = await adminApi.getTreino("treino-001");
    expect(res.data).toEqual(data);
  });
});

describe("adminApi.getTreinadorPacotes", () => {
  it("chama GET /admin/treinadores/{id}/pacotes", () => {
    adminApi.getTreinadorPacotes("t-789");
    expect(mockGet).toHaveBeenCalledWith("/admin/treinadores/t-789/pacotes");
  });

  it("retorna array de pacotes do apiClient", async () => {
    const data = [{ pacoteId: "p1", nome: "Básico", preco: 100 }];
    mockGet.mockResolvedValueOnce({ data });
    const res = await adminApi.getTreinadorPacotes("t-789");
    expect(res.data).toEqual(data);
  });
});

// ─── Treinadores — mutação ───────────────────────────────────────────────────

describe("adminApi.getTreinador", () => {
  it("chama GET /admin/treinadores/{id}", () => {
    adminApi.getTreinador("t-001");
    expect(mockGet).toHaveBeenCalledWith("/admin/treinadores/t-001");
  });
});

describe("adminApi.aprovarTreinador", () => {
  it("chama POST /admin/treinadores/{id}/aprovar com observacao", () => {
    adminApi.aprovarTreinador("t-001", "Aprovado");
    expect(mockPost).toHaveBeenCalledWith("/admin/treinadores/t-001/aprovar", { observacao: "Aprovado" });
  });

  it("observacao null quando omitida", () => {
    adminApi.aprovarTreinador("t-001");
    expect(mockPost).toHaveBeenCalledWith("/admin/treinadores/t-001/aprovar", { observacao: null });
  });
});

describe("adminApi.reprovarTreinador", () => {
  it("chama POST /admin/treinadores/{id}/reprovar com observacao", () => {
    adminApi.reprovarTreinador("t-001", "Documentação inválida");
    expect(mockPost).toHaveBeenCalledWith("/admin/treinadores/t-001/reprovar", { observacao: "Documentação inválida" });
  });

  it("observacao null quando omitida", () => {
    adminApi.reprovarTreinador("t-001");
    expect(mockPost).toHaveBeenCalledWith("/admin/treinadores/t-001/reprovar", { observacao: null });
  });
});

describe("adminApi.inativarTreinador", () => {
  it("chama POST /admin/treinadores/{id}/inativar com observacao", () => {
    adminApi.inativarTreinador("t-001", "Violação de termos");
    expect(mockPost).toHaveBeenCalledWith("/admin/treinadores/t-001/inativar", { observacao: "Violação de termos" });
  });

  it("observacao null quando omitida", () => {
    adminApi.inativarTreinador("t-001");
    expect(mockPost).toHaveBeenCalledWith("/admin/treinadores/t-001/inativar", { observacao: null });
  });
});

describe("adminApi.excluirTreinador", () => {
  it("chama DELETE /admin/treinadores/{id}", () => {
    adminApi.excluirTreinador("t-001");
    expect(mockDelete).toHaveBeenCalledWith("/admin/treinadores/t-001");
  });
});

describe("adminApi.atribuirPlano", () => {
  it("chama PATCH /admin/treinadores/{id}/plano com planoId", () => {
    adminApi.atribuirPlano("t-001", "plano-abc");
    expect(mockPatch).toHaveBeenCalledWith("/admin/treinadores/t-001/plano", { planoId: "plano-abc" });
  });
});

// ─── Planos ──────────────────────────────────────────────────────────────────

describe("adminApi.criarPlano", () => {
  it("chama POST /admin/planos com dados", () => {
    adminApi.criarPlano("Pro", "Pro", 50, 299.9);
    expect(mockPost).toHaveBeenCalledWith("/admin/planos", { nome: "Pro", tier: "Pro", maxAlunos: 50, preco: 299.9, descricao: undefined });
  });
});

describe("adminApi.atualizarPlano", () => {
  it("chama PATCH /admin/planos/{id} com dados parciais", () => {
    adminApi.atualizarPlano("plano-abc", { nome: "Pro Plus", maxAlunos: 100 });
    expect(mockPatch).toHaveBeenCalledWith("/admin/planos/plano-abc", { nome: "Pro Plus", maxAlunos: 100 });
  });
});

describe("adminApi.excluirPlano", () => {
  it("chama DELETE /admin/planos/{id}", () => {
    adminApi.excluirPlano("plano-abc");
    expect(mockDelete).toHaveBeenCalledWith("/admin/planos/plano-abc");
  });
});

// ─── Grupos Musculares ───────────────────────────────────────────────────────

describe("adminApi.criarGrupoMuscular", () => {
  it("chama POST /admin/grupos-musculares com nome", () => {
    adminApi.criarGrupoMuscular("Trapézio");
    expect(mockPost).toHaveBeenCalledWith("/admin/grupos-musculares", { nome: "Trapézio" });
  });
});

describe("adminApi.atualizarGrupoMuscular", () => {
  it("chama PATCH /admin/grupos-musculares/{id} com nome", () => {
    adminApi.atualizarGrupoMuscular("gm-01", "Trapézio Superior");
    expect(mockPatch).toHaveBeenCalledWith("/admin/grupos-musculares/gm-01", { nome: "Trapézio Superior" });
  });
});

describe("adminApi.excluirGrupoMuscular", () => {
  it("chama DELETE /admin/grupos-musculares/{id}", () => {
    adminApi.excluirGrupoMuscular("gm-01");
    expect(mockDelete).toHaveBeenCalledWith("/admin/grupos-musculares/gm-01");
  });
});

// ─── Exercícios globais ──────────────────────────────────────────────────────

describe("adminApi.listExerciciosGlobais", () => {
  it("chama GET /admin/exercicios sem params", () => {
    adminApi.listExerciciosGlobais();
    expect(mockGet).toHaveBeenCalledWith("/admin/exercicios", { params: undefined });
  });

  it("chama GET /admin/exercicios com filtros", () => {
    adminApi.listExerciciosGlobais({ nome: "Supino", grupoMuscularId: "gm-01", pagina: 1, tamanhoPagina: 20 });
    expect(mockGet).toHaveBeenCalledWith("/admin/exercicios", {
      params: { nome: "Supino", grupoMuscularId: "gm-01", pagina: 1, tamanhoPagina: 20 },
    });
  });
});

describe("adminApi.criarExercicioGlobal", () => {
  it("chama POST /admin/exercicios com dados", () => {
    adminApi.criarExercicioGlobal({ nome: "Supino Reto", grupoMuscularId: "gm-01", descricao: "Barra" });
    expect(mockPost).toHaveBeenCalledWith("/admin/exercicios", {
      nome: "Supino Reto", grupoMuscularId: "gm-01", descricao: "Barra",
    });
  });

  it("descricao null quando omitida", () => {
    adminApi.criarExercicioGlobal({ nome: "Agachamento", grupoMuscularId: "gm-02" });
    expect(mockPost).toHaveBeenCalledWith("/admin/exercicios", {
      nome: "Agachamento", grupoMuscularId: "gm-02",
    });
  });
});

describe("adminApi.atualizarExercicioGlobal", () => {
  it("chama PATCH /admin/exercicios/{id} com dados parciais", () => {
    adminApi.atualizarExercicioGlobal("ex-01", { nome: "Supino Inclinado" });
    expect(mockPatch).toHaveBeenCalledWith("/admin/exercicios/ex-01", { nome: "Supino Inclinado" });
  });
});

describe("adminApi.excluirExercicioGlobal", () => {
  it("chama DELETE /admin/exercicios/{id}", () => {
    adminApi.excluirExercicioGlobal("ex-01");
    expect(mockDelete).toHaveBeenCalledWith("/admin/exercicios/ex-01");
  });
});

// ─── Alunos — alteração de status ────────────────────────────────────────────

describe("adminApi.alterarStatusAluno", () => {
  it("chama PATCH /alunos/{id}/status com status Ativo", () => {
    adminApi.alterarStatusAluno("a-001", "Ativo");
    expect(mockPatch).toHaveBeenCalledWith("/alunos/a-001/status", { status: "Ativo" });
  });

  it("chama PATCH /alunos/{id}/status com status Inativo", () => {
    adminApi.alterarStatusAluno("a-001", "Inativo");
    expect(mockPatch).toHaveBeenCalledWith("/alunos/a-001/status", { status: "Inativo" });
  });
});

// ─── Compatibilidade com funções existentes não alteradas ────────────────────

describe("adminApi — funções preexistentes intactas", () => {
  it("listTreinadores chama GET /admin/treinadores", () => {
    adminApi.listTreinadores();
    expect(mockGet).toHaveBeenCalledWith("/admin/treinadores", { params: undefined });
  });

  it("listPlanos chama GET /admin/planos", () => {
    adminApi.listPlanos();
    expect(mockGet).toHaveBeenCalledWith("/admin/planos");
  });

  it("listGruposMusculares chama GET /admin/grupos-musculares", () => {
    adminApi.listGruposMusculares();
    expect(mockGet).toHaveBeenCalledWith("/admin/grupos-musculares");
  });
});
