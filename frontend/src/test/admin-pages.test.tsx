import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import type {
  AlunoResponse, TreinoResponse, MeuVinculoResponse,
  VinculoAlunoItemResponse, FichaAlunoResponse, PacoteAlunoResponse,
} from "@/types";

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
import { usePaginatedList } from "@/hooks/usePaginatedList";

const mockAdminApi = vi.mocked(adminApi);
const mockUsePaginatedList = vi.mocked(usePaginatedList);
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

// ─── Fixtures extras ─────────────────────────────────────────────────────────

const BASE_PAGINATED = {
  total: 0, page: 0, pageSize: 20, loading: false,
  error: "", success: "",
  setPage: vi.fn(), setPageSize: vi.fn(), setError: vi.fn(),
  setSuccess: vi.fn(), reload: vi.fn(),
};

const mockVinculoAtivo: VinculoAlunoItemResponse = {
  vinculoId: "v-1", treinadorId: "t-1",
  nomeTreinador: "Treinador Vinculado XYZ",
  status: "Ativo", dataInicio: "2025-01-01T00:00:00Z",
  createdAt: "2025-01-01T00:00:00Z",
};

const mockVinculoPendente: VinculoAlunoItemResponse = {
  vinculoId: "v-2", treinadorId: "t-2",
  nomeTreinador: "Novo Treinador ABC",
  status: "AguardandoAprovacao", dataInicio: null,
  createdAt: "2025-03-01T00:00:00Z",
};

// ─── AlunosAdminPage — renderCell e filtros ──────────────────────────────────

describe("AlunosAdminPage — renderCell com dados", () => {
  const mockAluno: AlunoResponse = {
    alunoId: "a-render-1", nome: "Pedro Render", email: "pedro@test.com",
    telefone: null, status: "Ativo", contaId: "c1",
    createdAt: "2025-01-10T00:00:00Z", updatedAt: null,
    diasDisponiveis: null, tempoDisponivelMinutos: null,
    finalidade: null, focoTreino: null, nivelCondicionamento: null,
    limitacoesFisicas: null, doencas: null, observacoesAdicionais: null,
  };

  beforeEach(() => {
    mockUsePaginatedList.mockReturnValue({ ...BASE_PAGINATED, items: [mockAluno], total: 1 });
  });

  it("renderiza nome do aluno (i=0)", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/page");
    render(<Page />);
    expect(screen.getByText("Pedro Render")).toBeDefined();
  });

  it("renderiza email do aluno (i=1)", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/page");
    render(<Page />);
    expect(screen.getByText("pedro@test.com")).toBeDefined();
  });

  it("renderiza status chip (i=2)", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/page");
    render(<Page />);
    // StatusChip mostra "Ativo"
    expect(screen.getAllByText("Ativo").length).toBeGreaterThan(0);
  });

  it("enter no campo de nome aplica filtro", async () => {
    const setPage = vi.fn();
    mockUsePaginatedList.mockReturnValue({ ...BASE_PAGINATED, items: [], setPage });
    const { default: Page } = await import("@/app/(admin)/admin/alunos/page");
    render(<Page />);
    const input = screen.getByLabelText("Buscar por nome");
    fireEvent.change(input, { target: { value: "Teste" } });
    fireEvent.keyDown(input, { key: "Enter" });
    expect(setPage).toHaveBeenCalledWith(0);
  });
});

// ─── DetalheAlunoAdminPage — tabs e vínculos ────────────────────────────────

describe("DetalheAlunoAdminPage — tabs", () => {
  const mockAluno: AlunoResponse = {
    alunoId: "aluno-001", nome: "Maria Silva", email: "maria@email.com",
    telefone: "11987654321", status: "Ativo", contaId: "conta-001",
    createdAt: "2024-01-15T00:00:00Z", updatedAt: null,
    diasDisponiveis: null, tempoDisponivelMinutos: null,
    finalidade: null, focoTreino: null, nivelCondicionamento: null,
    limitacoesFisicas: null, doencas: null, observacoesAdicionais: null,
  };

  beforeEach(() => {
    mockUseParams.mockReturnValue({ alunoId: "aluno-001" });
    mockAdminApi.getAluno.mockResolvedValue({ data: mockAluno } as never);
    mockAdminApi.getAlunoVinculo.mockResolvedValue({ data: { vinculoAtivo: null, vinculoPendente: null } } as never);
    mockAdminApi.getAlunoProgressao.mockResolvedValue({ data: { exercicios: [] } } as never);
    mockUsePaginatedList.mockReturnValue({ ...BASE_PAGINATED, items: [] });
  });

  it("tab Fichas → mostra empty state de fichas", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    await waitFor(() => expect(screen.getByText("Maria Silva")).toBeDefined());
    fireEvent.click(screen.getByRole("tab", { name: "Fichas" }));
    expect(screen.getByText("Nenhuma ficha vinculada a este aluno.")).toBeDefined();
  });

  it("tab Execuções → mostra empty state de execuções", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    await waitFor(() => expect(screen.getByText("Maria Silva")).toBeDefined());
    fireEvent.click(screen.getByRole("tab", { name: "Execuções" }));
    expect(screen.getByText("Nenhuma execução registrada para este aluno.")).toBeDefined();
  });

  it("tab Progressão → chama getAlunoProgressao e exibe empty state", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    await waitFor(() => expect(screen.getByText("Maria Silva")).toBeDefined());
    fireEvent.click(screen.getByRole("tab", { name: "Progressão" }));
    await waitFor(() =>
      expect(screen.getByText("Nenhuma execução registrada no período.")).toBeDefined(),
    );
    expect(mockAdminApi.getAlunoProgressao).toHaveBeenCalled();
  });

  it("vinculoAtivo → exibe nome do treinador", async () => {
    mockAdminApi.getAlunoVinculo.mockResolvedValue({
      data: { vinculoAtivo: mockVinculoAtivo, vinculoPendente: null },
    } as never);
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    await waitFor(() =>
      expect(screen.getByText("Treinador Vinculado XYZ")).toBeDefined(),
    );
  });

  it("vinculoPendente (sem ativo) → exibe nome do novo treinador", async () => {
    mockAdminApi.getAlunoVinculo.mockResolvedValue({
      data: { vinculoAtivo: null, vinculoPendente: mockVinculoPendente },
    } as never);
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    await waitFor(() =>
      expect(screen.getByText("Novo Treinador ABC")).toBeDefined(),
    );
  });

  it("sem vínculo → exibe mensagem sem vínculo", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    await waitFor(() =>
      expect(screen.getByText("Sem vínculo ativo.")).toBeDefined(),
    );
  });

  it("aluno com telefone → exibe celular formatado (11 dígitos)", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    await waitFor(() =>
      expect(screen.getByText("(11) 98765-4321")).toBeDefined(),
    );
  });

  it("aluno com perfil de treino → exibe seção Perfil de treino", async () => {
    mockAdminApi.getAluno.mockResolvedValue({
      data: { ...mockAluno, finalidade: "Hipertrofia", nivelCondicionamento: "Iniciante" },
    } as never);
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    await waitFor(() =>
      expect(screen.getByText("Perfil de treino")).toBeDefined(),
    );
  });
});

// ─── DetalheTreinadorAdminPage — tabs ────────────────────────────────────────

describe("DetalheTreinadorAdminPage — tabs e pacotes", () => {
  const mockTreinador = {
    treinadorId: "t-001", nome: "Carlos Ferreira",
    contaId: "conta-t-001", status: "Ativo" as const,
    planoTreinadorId: null, createdAt: "2024-01-01T00:00:00Z",
  };

  const mockPacote: PacoteAlunoResponse = {
    pacoteId: "p-1", nome: "Pacote Premium",
    descricao: "Acompanhamento completo", preco: 400,
    treinadorId: "t-001",
  };

  beforeEach(() => {
    mockUseParams.mockReturnValue({ treinadorId: "t-001" });
    mockAdminApi.getTreinador.mockResolvedValue({ data: mockTreinador } as never);
    mockAdminApi.getTreinadorPacotes.mockResolvedValue({ data: [mockPacote] } as never);
    mockUsePaginatedList.mockReturnValue({ ...BASE_PAGINATED, items: [] });
  });

  it("tab Vínculos → mostra empty state de vínculos", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/treinadores/[treinadorId]/page");
    render(<Page />);
    await waitFor(() => expect(screen.getByText("Carlos Ferreira")).toBeDefined());
    fireEvent.click(screen.getByRole("tab", { name: "Vínculos" }));
    expect(screen.getByText("Nenhum vínculo encontrado.")).toBeDefined();
  });

  it("tab Treinos → mostra empty state de treinos", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/treinadores/[treinadorId]/page");
    render(<Page />);
    await waitFor(() => expect(screen.getByText("Carlos Ferreira")).toBeDefined());
    fireEvent.click(screen.getByRole("tab", { name: "Treinos" }));
    expect(screen.getByText("Nenhum treino encontrado para este treinador.")).toBeDefined();
  });

  it("tab Pacotes → chama getTreinadorPacotes e exibe pacote", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/treinadores/[treinadorId]/page");
    render(<Page />);
    await waitFor(() => expect(screen.getByText("Carlos Ferreira")).toBeDefined());
    fireEvent.click(screen.getByRole("tab", { name: "Pacotes" }));
    await waitFor(() =>
      expect(screen.getByText("Pacote Premium")).toBeDefined(),
    );
    expect(mockAdminApi.getTreinadorPacotes).toHaveBeenCalledWith("t-001");
  });
});
