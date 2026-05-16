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

const mockGet = vi.mocked(apiClient.get);

beforeEach(() => {
  vi.clearAllMocks();
  mockGet.mockResolvedValue({ data: {} });
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
