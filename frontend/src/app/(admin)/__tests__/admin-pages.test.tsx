// F6e (Fase 3 test remediation) — migrado de vi.mock("@/lib/api/admin")
// pra MSW. apiClient real envia GET; MSW intercepta. Pega bugs de URL,
// params, e interceptor que mock antigo escondia.
//
// Mocks restantes (next/navigation, usePaginatedList hook, recharts) NAO sao
// @/lib/api/* — fora do scope F6.
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type {
  AlunoResponse, TreinoResponse, MeuVinculoResponse,
  VinculoAlunoItemResponse, PacoteResponse,
} from "@/types";

// ─── Mocks globais (nao API) ─────────────────────────────────────────────────

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

import { useParams } from "next/navigation";
import { usePaginatedList } from "@/hooks/usePaginatedList";

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

// ─── Helpers MSW ─────────────────────────────────────────────────────────────

function hang() {
  return new Promise<Response>(() => {});
}

// ─── AlunosAdminPage ─────────────────────────────────────────────────────────

describe("AlunosAdminPage", () => {
  beforeEach(() => {
    server.use(
      http.get("*/admin/alunos", () =>
        HttpResponse.json({ items: [], total: 0, pagina: 1, tamanhoPagina: 20 }),
      ),
    );
  });

  it("renderiza título 'Alunos'", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/page");
    render(<Page />);
    expect(screen.getByText("Alunos")).toBeInTheDocument();
  }, 15000);

  it("renderiza filtro de status", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/page");
    render(<Page />);
    const comboboxes = screen.getAllByRole("combobox");
    expect(comboboxes.length).toBeGreaterThanOrEqual(1);
  });

  it("renderiza campo de busca por nome", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/page");
    render(<Page />);
    expect(screen.getByLabelText("Buscar por nome")).toBeInTheDocument();
  });
});

// ─── DetalheAlunoAdminPage ───────────────────────────────────────────────────

describe("DetalheAlunoAdminPage", () => {
  beforeEach(() => {
    mockUseParams.mockReturnValue({ alunoId: "aluno-001" });
    server.use(
      http.get("*/admin/alunos/:id", () => HttpResponse.json(mockAluno)),
      http.get("*/admin/alunos/:id/vinculo", () => HttpResponse.json(mockVinculo)),
    );
  });

  it("exibe spinner durante carregamento", async () => {
    server.use(http.get("*/admin/alunos/:id", () => hang()));
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    expect(screen.getByRole("progressbar")).toBeInTheDocument();
  });

  it("exibe nome do aluno após carregamento", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    await waitFor(() => {
      expect(screen.getByText("Maria Silva")).toBeInTheDocument();
    });
  });

  it("exibe tabs Dados, Fichas, Execuções e Progressão", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    await waitFor(() => {
      expect(screen.getByText("Dados")).toBeInTheDocument();
      expect(screen.getByText("Fichas")).toBeInTheDocument();
      expect(screen.getByText("Execuções")).toBeInTheDocument();
      expect(screen.getByText("Progressão")).toBeInTheDocument();
    });
  });

  it("exibe status do aluno após carregamento", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    await waitFor(() => {
      expect(screen.getByText("Ativo")).toBeInTheDocument();
    });
  });

  it("exibe alerta de erro quando API falha", async () => {
    server.use(
      http.get("*/admin/alunos/:id", () => HttpResponse.json({ title: "fail" }, { status: 500 })),
    );
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    await waitFor(() => {
      expect(screen.getByText("Erro ao carregar dados do aluno.")).toBeInTheDocument();
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
    planoPlataformaId: null,
    createdAt: "2024-01-01T00:00:00Z",
  };

  beforeEach(() => {
    mockUseParams.mockReturnValue({ treinadorId: "t-001" });
    server.use(
      http.get("*/admin/treinadores/:id", () => HttpResponse.json(mockTreinador)),
    );
  });

  it("exibe spinner durante carregamento", async () => {
    server.use(http.get("*/admin/treinadores/:id", () => hang()));
    const { default: Page } = await import("@/app/(admin)/admin/treinadores/[treinadorId]/page");
    render(<Page />);
    expect(screen.getByRole("progressbar")).toBeInTheDocument();
  });

  it("exibe nome do treinador após carregamento", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/treinadores/[treinadorId]/page");
    render(<Page />);
    await waitFor(() => {
      expect(screen.getByText("Carlos Ferreira")).toBeInTheDocument();
    });
  });

  it("exibe tabs Alunos, Vínculos, Treinos e Pacotes", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/treinadores/[treinadorId]/page");
    render(<Page />);
    await waitFor(() => {
      expect(screen.getByText("Alunos")).toBeInTheDocument();
      expect(screen.getByText("Vínculos")).toBeInTheDocument();
      expect(screen.getByText("Treinos")).toBeInTheDocument();
      expect(screen.getByText("Pacotes")).toBeInTheDocument();
    });
  });

  it("exibe alerta de erro quando getTreinador falha", async () => {
    server.use(
      http.get("*/admin/treinadores/:id", () => HttpResponse.json({ title: "fail" }, { status: 500 })),
    );
    const { default: Page } = await import("@/app/(admin)/admin/treinadores/[treinadorId]/page");
    render(<Page />);
    await waitFor(() => {
      expect(screen.getByText("Erro ao carregar dados do treinador.")).toBeInTheDocument();
    });
  });
});

// ─── DetalheTreinoAdminPage ───────────────────────────────────────────────────

describe("DetalheTreinoAdminPage", () => {
  beforeEach(() => {
    mockUseParams.mockReturnValue({ treinoId: "treino-001" });
    server.use(
      http.get("*/admin/treinos/:id", () => HttpResponse.json(mockTreino)),
    );
  });

  it("exibe spinner durante carregamento", async () => {
    server.use(http.get("*/admin/treinos/:id", () => hang()));
    const { default: Page } = await import("@/app/(admin)/admin/treinos/[treinoId]/page");
    render(<Page />);
    expect(screen.getByRole("progressbar")).toBeInTheDocument();
  });

  it("exibe nome do treino após carregamento", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/treinos/[treinoId]/page");
    render(<Page />);
    await waitFor(() => {
      expect(screen.getByText("Treino de Costas")).toBeInTheDocument();
    });
  });

  it("exibe objetivo do treino", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/treinos/[treinoId]/page");
    render(<Page />);
    await waitFor(() => {
      expect(screen.getByText("Hipertrofia")).toBeInTheDocument();
    });
  });

  it("exibe nome do exercício na lista", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/treinos/[treinoId]/page");
    render(<Page />);
    await waitFor(() => {
      expect(screen.getByText("Barra Fixa")).toBeInTheDocument();
    });
  });

  it("exibe contador de exercícios", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/treinos/[treinoId]/page");
    render(<Page />);
    await waitFor(() => {
      expect(screen.getByText("Exercícios (1)")).toBeInTheDocument();
    });
  });

  it("exibe alerta de erro quando API falha", async () => {
    server.use(
      http.get("*/admin/treinos/:id", () => HttpResponse.json({ title: "fail" }, { status: 404 })),
    );
    const { default: Page } = await import("@/app/(admin)/admin/treinos/[treinoId]/page");
    render(<Page />);
    await waitFor(() => {
      expect(screen.getByText("Erro ao carregar treino.")).toBeInTheDocument();
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
  const mockAlunoRender: AlunoResponse = {
    alunoId: "a-render-1", nome: "Pedro Render", email: "pedro@test.com",
    telefone: null, status: "Ativo", contaId: "c1",
    createdAt: "2025-01-10T00:00:00Z", updatedAt: null,
    diasDisponiveis: null, tempoDisponivelMinutos: null,
    finalidade: null, focoTreino: null, nivelCondicionamento: null,
    limitacoesFisicas: null, doencas: null, observacoesAdicionais: null,
  };

  beforeEach(() => {
    mockUsePaginatedList.mockReturnValue({ ...BASE_PAGINATED, items: [mockAlunoRender], total: 1 });
  });

  it("renderiza nome do aluno (i=0)", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/page");
    render(<Page />);
    expect(screen.getByText("Pedro Render")).toBeInTheDocument();
  });

  it("renderiza email do aluno (i=1)", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/page");
    render(<Page />);
    expect(screen.getByText("pedro@test.com")).toBeInTheDocument();
  });

  it("renderiza status chip (i=2)", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/page");
    render(<Page />);
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
  const mockAlunoTabs: AlunoResponse = {
    alunoId: "aluno-001", nome: "Maria Silva", email: "maria@email.com",
    telefone: "11987654321", status: "Ativo", contaId: "conta-001",
    createdAt: "2024-01-15T00:00:00Z", updatedAt: null,
    diasDisponiveis: null, tempoDisponivelMinutos: null,
    finalidade: null, focoTreino: null, nivelCondicionamento: null,
    limitacoesFisicas: null, doencas: null, observacoesAdicionais: null,
  };

  let progressaoCalled = false;

  beforeEach(() => {
    mockUseParams.mockReturnValue({ alunoId: "aluno-001" });
    progressaoCalled = false;
    server.use(
      http.get("*/admin/alunos/:id", () => HttpResponse.json(mockAlunoTabs)),
      http.get("*/admin/alunos/:id/vinculo", () =>
        HttpResponse.json({ vinculoAtivo: null, vinculoPendente: null }),
      ),
      http.get("*/admin/alunos/:id/progressao", () => {
        progressaoCalled = true;
        return HttpResponse.json({ exercicios: [] });
      }),
    );
    mockUsePaginatedList.mockReturnValue({ ...BASE_PAGINATED, items: [] });
  });

  it("tab Fichas → mostra empty state de fichas", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    expect(await screen.findByText("Maria Silva")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("tab", { name: "Fichas" }));
    expect(screen.getByText("Nenhuma ficha vinculada a este aluno.")).toBeInTheDocument();
  });

  it("tab Execuções → mostra empty state de execuções", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    expect(await screen.findByText("Maria Silva")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("tab", { name: "Execuções" }));
    expect(screen.getByText("Nenhuma execução registrada para este aluno.")).toBeInTheDocument();
  });

  it("tab Progressão → chama getAlunoProgressao e exibe empty state", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    expect(await screen.findByText("Maria Silva")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("tab", { name: "Progressão" }));
    expect(await screen.findByText("Nenhuma execução registrada no período.")).toBeInTheDocument();
    expect(progressaoCalled).toBe(true);
  });

  it("vinculoAtivo → exibe nome do treinador", async () => {
    server.use(
      http.get("*/admin/alunos/:id/vinculo", () =>
        HttpResponse.json({ vinculoAtivo: mockVinculoAtivo, vinculoPendente: null }),
      ),
    );
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    expect(await screen.findByText("Treinador Vinculado XYZ")).toBeInTheDocument();
  });

  it("vinculoPendente (sem ativo) → exibe nome do novo treinador", async () => {
    server.use(
      http.get("*/admin/alunos/:id/vinculo", () =>
        HttpResponse.json({ vinculoAtivo: null, vinculoPendente: mockVinculoPendente }),
      ),
    );
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    expect(await screen.findByText("Novo Treinador ABC")).toBeInTheDocument();
  });

  it("sem vínculo → exibe mensagem sem vínculo", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    expect(await screen.findByText("Sem vínculo ativo.")).toBeInTheDocument();
  });

  it("aluno com telefone → exibe celular formatado (11 dígitos)", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    expect(await screen.findByText("(11) 98765-4321")).toBeInTheDocument();
  });

  it("aluno com perfil de treino → exibe seção Perfil de treino", async () => {
    server.use(
      http.get("*/admin/alunos/:id", () =>
        HttpResponse.json({ ...mockAlunoTabs, finalidade: "Hipertrofia", nivelCondicionamento: "Iniciante" }),
      ),
    );
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    expect(await screen.findByText("Perfil de treino")).toBeInTheDocument();
  });
});

// ─── DetalheAlunoAdminPage — progressão com dados e formatPhone ──────────────

describe("DetalheAlunoAdminPage — progressão com dados", () => {
  const mockAlunoTel10: AlunoResponse = {
    alunoId: "aluno-tel10", nome: "Tel Dez", email: "tel@test.com",
    telefone: "1198765432",
    status: "Ativo", contaId: "c1",
    createdAt: "2024-01-15T00:00:00Z", updatedAt: null,
    diasDisponiveis: null, tempoDisponivelMinutos: null,
    finalidade: null, focoTreino: null, nivelCondicionamento: null,
    limitacoesFisicas: null, doencas: null, observacoesAdicionais: null,
  };

  beforeEach(() => {
    mockUseParams.mockReturnValue({ alunoId: "aluno-tel10" });
    server.use(
      http.get("*/admin/alunos/:id/vinculo", () =>
        HttpResponse.json({ vinculoAtivo: null, vinculoPendente: null }),
      ),
    );
    mockUsePaginatedList.mockReturnValue({ ...BASE_PAGINATED, items: [] });
  });

  it("aluno com telefone 10 dígitos → exibe formato (XX) XXXX-XXXX", async () => {
    server.use(http.get("*/admin/alunos/:id", () => HttpResponse.json(mockAlunoTel10)));
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    expect(await screen.findByText("(11) 9876-5432")).toBeInTheDocument();
  });

  it("progressão com exercícios → renderiza card de exercício", async () => {
    server.use(
      http.get("*/admin/alunos/:id", () => HttpResponse.json(mockAlunoTel10)),
      http.get("*/admin/alunos/:id/progressao", () =>
        HttpResponse.json({
          exercicios: [{
            nomeExercicio: "Supino Reto",
            grupoMuscular: "Peitoral",
            historico: [
              { data: "2025-05-01", cargaMaxima: 60, seriesExecutadas: 4, repeticoesExecutadas: 10 },
              { data: "2025-05-08", cargaMaxima: 65, seriesExecutadas: 4, repeticoesExecutadas: 10 },
            ],
          }],
        }),
      ),
    );
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);
    expect(await screen.findByText("Tel Dez")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("tab", { name: "Progressão" }));
    expect(await screen.findByText("Supino Reto")).toBeInTheDocument();
    expect(screen.getByText("Peitoral")).toBeInTheDocument();
    expect(screen.getByText("Último: 65 kg")).toBeInTheDocument();
  });
});

// ─── DetalheTreinadorAdminPage — tabs ────────────────────────────────────────

describe("DetalheTreinadorAdminPage — tabs e pacotes", () => {
  const mockTreinador = {
    treinadorId: "t-001", nome: "Carlos Ferreira",
    contaId: "conta-t-001", status: "Ativo" as const,
    planoPlataformaId: null, createdAt: "2024-01-01T00:00:00Z",
  };

  const mockPacote: PacoteResponse = {
    pacoteId: "p-1", nome: "Pacote Premium",
    descricao: "Acompanhamento completo", preco: 400,
    treinadorId: "t-001",
  };

  let pacotesPath: string | null = null;

  beforeEach(() => {
    mockUseParams.mockReturnValue({ treinadorId: "t-001" });
    pacotesPath = null;
    server.use(
      http.get("*/admin/treinadores/:id", () => HttpResponse.json(mockTreinador)),
      http.get("*/admin/treinadores/:id/pacotes", ({ request }) => {
        pacotesPath = new URL(request.url).pathname;
        return HttpResponse.json([mockPacote]);
      }),
    );
    mockUsePaginatedList.mockReturnValue({ ...BASE_PAGINATED, items: [] });
  });

  it("tab Vínculos → mostra empty state de vínculos", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/treinadores/[treinadorId]/page");
    render(<Page />);
    expect(await screen.findByText("Carlos Ferreira")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("tab", { name: "Vínculos" }));
    expect(screen.getByText("Nenhum vínculo encontrado.")).toBeInTheDocument();
  });

  it("tab Treinos → mostra empty state de treinos", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/treinadores/[treinadorId]/page");
    render(<Page />);
    expect(await screen.findByText("Carlos Ferreira")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("tab", { name: "Treinos" }));
    expect(screen.getByText("Nenhum treino encontrado para este treinador.")).toBeInTheDocument();
  });

  it("tab Pacotes → chama getTreinadorPacotes (URL captura) e exibe pacote", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/treinadores/[treinadorId]/page");
    render(<Page />);
    expect(await screen.findByText("Carlos Ferreira")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("tab", { name: "Pacotes" }));
    expect(await screen.findByText("Pacote Premium")).toBeInTheDocument();
    // Captura via MSW handler: verifica URL contém treinadorId correto.
    expect(pacotesPath).toContain("/admin/treinadores/t-001/pacotes");
  });

  it("tab Pacotes → sem pacotes exibe 'Nenhum pacote cadastrado.'", async () => {
    server.use(
      http.get("*/admin/treinadores/:id/pacotes", () => HttpResponse.json([])),
    );
    const { default: Page } = await import("@/app/(admin)/admin/treinadores/[treinadorId]/page");
    render(<Page />);
    expect(await screen.findByText("Carlos Ferreira")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("tab", { name: "Pacotes" }));
    expect(await screen.findByText("Nenhum pacote cadastrado.")).toBeInTheDocument();
  });

  it("tab Alunos → mostra empty state", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/treinadores/[treinadorId]/page");
    render(<Page />);
    expect(await screen.findByText("Carlos Ferreira")).toBeInTheDocument();
    expect(screen.getByText("Este treinador não possui alunos ativos.")).toBeInTheDocument();
  });
});
