import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import type { AlunoResponse, TreinoResponse, MeuVinculoResponse } from "@/types";

// ─── Mocks globais ────────────────────────────────────────────────────────────

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn() })),
  useParams: vi.fn(() => ({})),
}));

vi.mock("@/hooks/usePaginatedList", () => ({
  usePaginatedList: vi.fn(() => ({
    items: [],
    total: 0,
    page: 0,
    pageSize: 20,
    loading: false,
    error: "",
    success: "",
    setPage: vi.fn(),
    setPageSize: vi.fn(),
    setError: vi.fn(),
    setSuccess: vi.fn(),
    reload: vi.fn(),
  })),
}));

vi.mock("recharts", () => ({
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  LineChart: () => null,
  Line: () => null,
  XAxis: () => null,
  YAxis: () => null,
  CartesianGrid: () => null,
  Tooltip: () => null,
  BarChart: () => null,
  Bar: () => null,
}));

vi.mock("@/lib/api/admin", () => ({
  adminApi: {
    listAlunos: vi.fn(),
    getAluno: vi.fn(),
    getTreinador: vi.fn(),
    getAlunoVinculo: vi.fn(),
    getAlunoFichas: vi.fn(),
    getAlunoExecucoes: vi.fn(),
    getAlunoProgressao: vi.fn(),
    getTreinadorAlunos: vi.fn(),
    getTreinadorVinculos: vi.fn(),
    getTreinadorTreinos: vi.fn(),
    getTreinadorPacotes: vi.fn(),
    getTreino: vi.fn(),
    listTreinadores: vi.fn(),
  },
}));

import { adminApi } from "@/lib/api/admin";
import { useParams } from "next/navigation";

const mockAdminApi = vi.mocked(adminApi);
const mockUseParams = vi.mocked(useParams);

// ─── Fixtures ────────────────────────────────────────────────────────────────

const mockAluno: AlunoResponse = {
  alunoId: "aluno-001",
  nome: "Maria Silva",
  email: "maria@email.com",
  telefone: null,
  status: "Ativo",
  contaId: "conta-001",
  createdAt: "2024-01-15T00:00:00Z",
  updatedAt: null,
  diasDisponiveis: null,
  tempoDisponivelMinutos: null,
  finalidade: null,
  focoTreino: null,
  nivelCondicionamento: null,
  limitacoesFisicas: null,
  doencas: null,
  observacoesAdicionais: null,
};

const mockVinculo: MeuVinculoResponse = {
  vinculoAtivo: null,
  vinculoPendente: null,
};

const mockTreino: TreinoResponse = {
  treinoId: "treino-001",
  nome: "Treino de Costas",
  objetivo: "Hipertrofia",
  dificuldade: "Intermediario",
  dataInicio: null,
  dataFim: null,
  treinadorId: "t-001",
  exercicios: [
    {
      treinoExercicioId: "ex-001",
      exercicioId: "e-001",
      nomeExercicio: "Barra Fixa",
      series: [
        {
          serieConfigId: "s-001",
          quantidade: 4,
          repeticoesMin: 8,
          repeticoesMax: 12,
          descricao: null,
          carga: null,
          descanso: 90,
          ordem: 1,
        },
      ],
      ordem: 1,
      observacao: null,
    },
  ],
  createdAt: "2024-02-01T00:00:00Z",
  updatedAt: null,
};

// ─── AlunosAdminPage ─────────────────────────────────────────────────────────

describe("AlunosAdminPage", () => {
  beforeEach(() => {
    mockAdminApi.listAlunos.mockResolvedValue({ data: { items: [], total: 0, pagina: 1, tamanhoPagina: 20 } } as never);
  });

  it("renderiza título 'Alunos'", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/page");
    render(<Page />);
    expect(screen.getByText("Alunos")).toBeDefined();
  });

  it("renderiza filtro de status", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/page");
    render(<Page />);
    // MUI Select renders a combobox — label is not associated via `for`
    expect(screen.getByRole("combobox")).toBeDefined();
  });

  it("renderiza campo de busca por nome", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/page");
    render(<Page />);
    expect(screen.getByLabelText("Buscar por nome")).toBeDefined();
  });
});

// ─── DetalheAlunoAdminPage ───────────────────────────────────────────────────

describe("DetalheAlunoAdminPage", () => {
  beforeEach(() => {
    mockUseParams.mockReturnValue({ alunoId: "aluno-001" });
    mockAdminApi.getAluno.mockResolvedValue({ data: mockAluno } as never);
    mockAdminApi.getAlunoVinculo.mockResolvedValue({ data: mockVinculo } as never);
  });

  it("exibe spinner durante carregamento", async () => {
    mockAdminApi.getAluno.mockImplementation(() => new Promise(() => {}));
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    expect(screen.getByRole("progressbar")).toBeDefined();
  });

  it("exibe nome do aluno após carregamento", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    await waitFor(() => {
      expect(screen.getByText("Maria Silva")).toBeDefined();
    });
  });

  it("exibe tabs Dados, Fichas, Execuções e Progressão", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    await waitFor(() => {
      expect(screen.getByText("Dados")).toBeDefined();
      expect(screen.getByText("Fichas")).toBeDefined();
      expect(screen.getByText("Execuções")).toBeDefined();
      expect(screen.getByText("Progressão")).toBeDefined();
    });
  });

  it("exibe status do aluno após carregamento", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    await waitFor(() => {
      expect(screen.getByText("Ativo")).toBeDefined();
    });
  });

  it("exibe alerta de erro quando API falha", async () => {
    mockAdminApi.getAluno.mockRejectedValue(new Error("Network error"));
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    await waitFor(() => {
      expect(screen.getByText("Erro ao carregar dados do aluno.")).toBeDefined();
    });
  });
});

// ─── DetalheTreinadorAdminPage ───────────────────────────────────────────────

describe("DetalheTreinadorAdminPage", () => {
  const mockTreinador = {
    treinadorId: "t-001",
    nome: "Carlos Ferreira",
    contaId: "conta-t-001",
    status: "Ativo" as const,
    planoTreinadorId: null,
    createdAt: "2024-01-01T00:00:00Z",
  };

  beforeEach(() => {
    mockUseParams.mockReturnValue({ treinadorId: "t-001" });
    mockAdminApi.getTreinador.mockResolvedValue({ data: mockTreinador } as never);
  });

  it("exibe spinner durante carregamento", async () => {
    mockAdminApi.getTreinador.mockImplementation(() => new Promise(() => {}));
    const { default: Page } = await import("@/app/(admin)/admin/treinadores/[treinadorId]/page");
    render(<Page />);
    expect(screen.getByRole("progressbar")).toBeDefined();
  });

  it("exibe nome do treinador após carregamento", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/treinadores/[treinadorId]/page");
    render(<Page />);
    await waitFor(() => {
      expect(screen.getByText("Carlos Ferreira")).toBeDefined();
    });
  });

  it("exibe tabs Alunos, Vínculos, Treinos e Pacotes", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/treinadores/[treinadorId]/page");
    render(<Page />);
    await waitFor(() => {
      expect(screen.getByText("Alunos")).toBeDefined();
      expect(screen.getByText("Vínculos")).toBeDefined();
      expect(screen.getByText("Treinos")).toBeDefined();
      expect(screen.getByText("Pacotes")).toBeDefined();
    });
  });

  it("exibe alerta de erro quando getTreinador falha", async () => {
    mockAdminApi.getTreinador.mockRejectedValue(new Error("fail"));
    const { default: Page } = await import("@/app/(admin)/admin/treinadores/[treinadorId]/page");
    render(<Page />);
    await waitFor(() => {
      expect(screen.getByText("Erro ao carregar dados do treinador.")).toBeDefined();
    });
  });
});

// ─── DetalheTreinoAdminPage ───────────────────────────────────────────────────

describe("DetalheTreinoAdminPage", () => {
  beforeEach(() => {
    mockUseParams.mockReturnValue({ treinoId: "treino-001" });
    mockAdminApi.getTreino.mockResolvedValue({ data: mockTreino } as never);
  });

  it("exibe spinner durante carregamento", async () => {
    mockAdminApi.getTreino.mockImplementation(() => new Promise(() => {}));
    const { default: Page } = await import("@/app/(admin)/admin/treinos/[treinoId]/page");
    render(<Page />);
    expect(screen.getByRole("progressbar")).toBeDefined();
  });

  it("exibe nome do treino após carregamento", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/treinos/[treinoId]/page");
    render(<Page />);
    await waitFor(() => {
      expect(screen.getByText("Treino de Costas")).toBeDefined();
    });
  });

  it("exibe objetivo do treino", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/treinos/[treinoId]/page");
    render(<Page />);
    await waitFor(() => {
      expect(screen.getByText("Hipertrofia")).toBeDefined();
    });
  });

  it("exibe nome do exercício na lista", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/treinos/[treinoId]/page");
    render(<Page />);
    await waitFor(() => {
      expect(screen.getByText("Barra Fixa")).toBeDefined();
    });
  });

  it("exibe contador de exercícios", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/treinos/[treinoId]/page");
    render(<Page />);
    await waitFor(() => {
      expect(screen.getByText("Exercícios (1)")).toBeDefined();
    });
  });

  it("exibe alerta de erro quando API falha", async () => {
    mockAdminApi.getTreino.mockRejectedValue(new Error("Not found"));
    const { default: Page } = await import("@/app/(admin)/admin/treinos/[treinoId]/page");
    render(<Page />);
    await waitFor(() => {
      expect(screen.getByText("Erro ao carregar treino.")).toBeDefined();
    });
  });
});
