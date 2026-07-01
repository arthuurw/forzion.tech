import React from "react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import type {
  AlunoResponse, MeuVinculoResponse, VinculoAlunoItemResponse,
  FichaAlunoResponse, ExecucaoTreinoResponse, ProgressaoAlunoResponse,
  PaginatedResponse,
} from "@/types";

const mockPush = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: mockPush, back: vi.fn(), replace: vi.fn() })),
  useParams: vi.fn(() => ({ alunoId: "a-1" })),
}));

vi.mock("recharts", () => ({
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  LineChart: ({ children }: { children: React.ReactNode }) => <svg role="img">{children}</svg>,
  Line: () => null,
  BarChart: ({ children }: { children: React.ReactNode }) => <svg role="img">{children}</svg>,
  Bar: () => null,
  XAxis: () => null,
  YAxis: () => null,
  CartesianGrid: () => null,
  Tooltip: () => null,
  Legend: () => null,
}));

vi.mock("@/lib/api/admin", () => ({
  adminApi: {
    getAluno: vi.fn(),
    getAlunoVinculo: vi.fn(),
    getAlunoFichas: vi.fn(),
    getAlunoExecucoes: vi.fn(),
    getAlunoProgressao: vi.fn(),
    exportarDadosConta: vi.fn(),
    anonimizarConta: vi.fn(),
  },
}));

import { adminApi } from "@/lib/api/admin";

const ALUNO: AlunoResponse = {
  alunoId: "a-1",
  nome: "Maria Aluna",
  email: "maria@example.com",
  telefone: null,
  status: "Ativo",
  contaId: "conta-1",
  createdAt: "2026-01-01T00:00:00Z",
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

const VINCULO_ATIVO: VinculoAlunoItemResponse = {
  vinculoId: "v-1",
  treinadorId: "tr-1",
  nomeTreinador: "Prof. Carlos",
  status: "Ativo",
  dataInicio: "2026-01-05T00:00:00Z",
  createdAt: "2026-01-01T00:00:00Z",
};

const VINCULO_PENDENTE: VinculoAlunoItemResponse = {
  vinculoId: "v-2",
  treinadorId: "tr-2",
  nomeTreinador: "Profa. Ana",
  status: "AguardandoAprovacao",
  dataInicio: null,
  createdAt: "2026-01-01T00:00:00Z",
};

const SEM_VINCULO: MeuVinculoResponse = { vinculoAtivo: null, vinculoPendente: null };

const FICHA: FichaAlunoResponse = {
  treinoAlunoId: "ta-1",
  treinoId: "t-1",
  nomeTreino: "Treino Full Body",
  objetivo: "Hipertrofia",
  status: "Ativo",
  exercicios: [],
};

const EXECUCAO: ExecucaoTreinoResponse = {
  execucaoId: "ex-1",
  treinoId: "t-1",
  alunoId: "a-1",
  dataExecucao: "2026-01-10T00:00:00Z",
  observacao: null,
  createdAt: "2026-01-10T00:00:00Z",
  nomeTreino: "Treino Full Body",
  totalExercicios: 3,
  totalSeries: 9,
};

const PROGRESSAO: ProgressaoAlunoResponse = {
  exercicios: [
    {
      nomeExercicio: "Supino reto",
      grupoMuscular: "Peito",
      historico: [
        { data: "2026-01-01", cargaMaxima: 60, seriesExecutadas: 3, repeticoesExecutadas: 10 },
        { data: "2026-01-08", cargaMaxima: 65, seriesExecutadas: 3, repeticoesExecutadas: 10 },
      ],
    },
  ],
};

const emptyFichas: PaginatedResponse<FichaAlunoResponse> = { items: [], total: 0, pagina: 1, tamanhoPagina: 20 };
const emptyExecucoes: PaginatedResponse<ExecucaoTreinoResponse> = { items: [], total: 0, pagina: 1, tamanhoPagina: 20 };

type Resolved<F extends (...args: never[]) => Promise<unknown>> = Awaited<ReturnType<F>>;

function asAluno(data: AlunoResponse) {
  return { data } as Resolved<typeof adminApi.getAluno>;
}
function asVinculo(data: MeuVinculoResponse) {
  return { data } as Resolved<typeof adminApi.getAlunoVinculo>;
}
function asFichas(data: PaginatedResponse<FichaAlunoResponse>) {
  return { data } as Resolved<typeof adminApi.getAlunoFichas>;
}
function asExecucoes(data: PaginatedResponse<ExecucaoTreinoResponse>) {
  return { data } as Resolved<typeof adminApi.getAlunoExecucoes>;
}
function asProgressao(data: ProgressaoAlunoResponse) {
  return { data } as Resolved<typeof adminApi.getAlunoProgressao>;
}
function asExport(data: Blob) {
  return { data } as Resolved<typeof adminApi.exportarDadosConta>;
}
function asAnonimizar() {
  return { data: undefined } as Resolved<typeof adminApi.anonimizarConta>;
}

beforeEach(() => {
  vi.mocked(adminApi.getAluno).mockResolvedValue(asAluno(ALUNO));
  vi.mocked(adminApi.getAlunoVinculo).mockResolvedValue(asVinculo(SEM_VINCULO));
  vi.mocked(adminApi.getAlunoFichas).mockResolvedValue(asFichas(emptyFichas));
  vi.mocked(adminApi.getAlunoExecucoes).mockResolvedValue(asExecucoes(emptyExecucoes));
  vi.mocked(adminApi.getAlunoProgressao).mockResolvedValue(asProgressao(PROGRESSAO));
});

afterEach(() => {
  vi.clearAllMocks();
});

async function renderPage() {
  const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
  const utils = render(<Page />);
  await waitFor(() => {
    expect(screen.queryByRole("progressbar")).not.toBeInTheDocument();
  });
  return utils;
}

describe("DetalheAlunoAdminPage — vínculo (aba Dados)", () => {
  it("vínculo ativo mostra o treinador, o status e a data de início", async () => {
    vi.mocked(adminApi.getAlunoVinculo).mockResolvedValue(
      asVinculo({ vinculoAtivo: VINCULO_ATIVO, vinculoPendente: null }),
    );
    await renderPage();

    expect(screen.getByText(/Prof\. Carlos/)).toBeInTheDocument();
    expect(screen.getByText(/Início:/)).toBeInTheDocument();
  });

  it("vínculo pendente mostra 'Treinador (pendente)' com o nome do treinador", async () => {
    vi.mocked(adminApi.getAlunoVinculo).mockResolvedValue(
      asVinculo({ vinculoAtivo: null, vinculoPendente: VINCULO_PENDENTE }),
    );
    await renderPage();

    expect(screen.getByText(/Treinador \(pendente\):/)).toBeInTheDocument();
    expect(screen.getByText(/Profa\. Ana/)).toBeInTheDocument();
  });

  it("sem vínculo ativo nem pendente mostra 'Sem vínculo ativo.'", async () => {
    await renderPage();

    expect(screen.getByText("Sem vínculo ativo.")).toBeInTheDocument();
  });
});

describe("DetalheAlunoAdminPage — abas Fichas e Execuções", () => {
  it("aba Fichas lista as fichas vinculadas ao trocar de aba", async () => {
    vi.mocked(adminApi.getAlunoFichas).mockResolvedValue(
      asFichas({ items: [FICHA], total: 1, pagina: 1, tamanhoPagina: 20 }),
    );
    await renderPage();

    fireEvent.click(screen.getByRole("tab", { name: "Fichas" }));

    await waitFor(() => {
      expect(screen.getByText("Treino Full Body")).toBeInTheDocument();
    });
  });

  it("aba Fichas sem fichas mostra o EmptyState", async () => {
    await renderPage();

    fireEvent.click(screen.getByRole("tab", { name: "Fichas" }));

    await waitFor(() => {
      expect(screen.getByText("Nenhuma ficha vinculada a este aluno.")).toBeInTheDocument();
    });
  });

  it("aba Execuções lista as execuções registradas ao trocar de aba", async () => {
    vi.mocked(adminApi.getAlunoExecucoes).mockResolvedValue(
      asExecucoes({ items: [EXECUCAO], total: 1, pagina: 1, tamanhoPagina: 20 }),
    );
    await renderPage();

    fireEvent.click(screen.getByRole("tab", { name: "Execuções" }));

    await waitFor(() => {
      expect(screen.getByText("3 exerc.")).toBeInTheDocument();
    });
    expect(screen.getByText("9 séries")).toBeInTheDocument();
  });

  it("aba Execuções sem execuções mostra o EmptyState", async () => {
    await renderPage();

    fireEvent.click(screen.getByRole("tab", { name: "Execuções" }));

    await waitFor(() => {
      expect(screen.getByText("Nenhuma execução registrada para este aluno.")).toBeInTheDocument();
    });
  });
});

describe("DetalheAlunoAdminPage — aba Progressão", () => {
  it("carrega apenas no primeiro acesso à aba e exibe Skeleton enquanto carrega", async () => {
    let resolveProgressao!: (v: Awaited<ReturnType<typeof adminApi.getAlunoProgressao>>) => void;
    vi.mocked(adminApi.getAlunoProgressao).mockReturnValue(
      new Promise((resolve) => { resolveProgressao = resolve; }),
    );

    const { container } = await renderPage();

    fireEvent.click(screen.getByRole("tab", { name: "Progressão" }));

    await waitFor(() => {
      expect(container.querySelectorAll(".MuiSkeleton-root").length).toBeGreaterThan(0);
    });
    expect(adminApi.getAlunoProgressao).toHaveBeenCalledTimes(1);

    resolveProgressao(asProgressao(PROGRESSAO));

    await waitFor(() => {
      expect(container.querySelectorAll(".MuiSkeleton-root").length).toBe(0);
    });
    expect(screen.getByText("Supino reto")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("tab", { name: "Dados" }));
    fireEvent.click(screen.getByRole("tab", { name: "Progressão" }));

    expect(adminApi.getAlunoProgressao).toHaveBeenCalledTimes(1);
  });

  it("sem execuções no período mostra o EmptyState", async () => {
    vi.mocked(adminApi.getAlunoProgressao).mockResolvedValue(asProgressao({ exercicios: [] }));
    await renderPage();

    fireEvent.click(screen.getByRole("tab", { name: "Progressão" }));

    await waitFor(() => {
      expect(screen.getByText("Nenhuma execução registrada no período.")).toBeInTheDocument();
    });
  });

  it("com dados exibe o card do exercício com nome, grupo muscular e a última carga", async () => {
    await renderPage();

    fireEvent.click(screen.getByRole("tab", { name: "Progressão" }));

    await waitFor(() => {
      expect(screen.getByText("Supino reto")).toBeInTheDocument();
    });
    expect(screen.getByText("Peito")).toBeInTheDocument();
    expect(screen.getByText(/Último: 65 kg/)).toBeInTheDocument();
  });

  it("trocar o período dispara nova chamada com de/ate diferentes", async () => {
    await renderPage();

    fireEvent.click(screen.getByRole("tab", { name: "Progressão" }));
    await waitFor(() => {
      expect(adminApi.getAlunoProgressao).toHaveBeenCalledTimes(1);
    });
    const [, primeiroParams] = vi.mocked(adminApi.getAlunoProgressao).mock.calls[0];

    fireEvent.click(screen.getByRole("button", { name: "60 dias" }));

    await waitFor(() => {
      expect(adminApi.getAlunoProgressao).toHaveBeenCalledTimes(2);
    });
    const [, segundoParams] = vi.mocked(adminApi.getAlunoProgressao).mock.calls[1];

    expect(segundoParams).not.toEqual(primeiroParams);
  });
});

describe("DetalheAlunoAdminPage — aba LGPD", () => {
  const originalCreateObjectURL = URL.createObjectURL;
  const originalRevokeObjectURL = URL.revokeObjectURL;

  beforeEach(() => {
    URL.createObjectURL = vi.fn(() => "blob:mock-url");
    URL.revokeObjectURL = vi.fn();
    vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {});
  });

  afterEach(() => {
    URL.createObjectURL = originalCreateObjectURL;
    URL.revokeObjectURL = originalRevokeObjectURL;
    vi.restoreAllMocks();
  });

  it("Exportar dados mostra 'Exportando...', chama exportarDadosConta e dispara o download", async () => {
    let resolveExport!: (v: Awaited<ReturnType<typeof adminApi.exportarDadosConta>>) => void;
    vi.mocked(adminApi.exportarDadosConta).mockReturnValue(
      new Promise((resolve) => { resolveExport = resolve; }),
    );

    await renderPage();
    fireEvent.click(screen.getByRole("tab", { name: "LGPD" }));

    const blob = new Blob(['{"nome":"Maria"}'], { type: "application/json" });
    fireEvent.click(screen.getByRole("button", { name: "Exportar dados (LGPD)" }));

    await waitFor(() => {
      expect(screen.getByRole("button", { name: "Exportando..." })).toBeInTheDocument();
    });
    expect(adminApi.exportarDadosConta).toHaveBeenCalledWith("conta-1");

    resolveExport(asExport(blob));

    await waitFor(() => {
      expect(screen.getByRole("button", { name: "Exportar dados (LGPD)" })).toBeInTheDocument();
    });
    expect(URL.createObjectURL).toHaveBeenCalledWith(blob);
    expect(HTMLAnchorElement.prototype.click).toHaveBeenCalledTimes(1);
  });

  it("Anonimizar: confirmar chama anonimizarConta e navega para a lista de alunos", async () => {
    vi.mocked(adminApi.anonimizarConta).mockResolvedValue(asAnonimizar());
    await renderPage();
    fireEvent.click(screen.getByRole("tab", { name: "LGPD" }));

    fireEvent.click(screen.getByRole("button", { name: "Anonimizar conta (LGPD)" }));
    await waitFor(() => {
      expect(screen.getByRole("dialog")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole("button", { name: "Anonimizar" }));

    await waitFor(() => {
      expect(adminApi.anonimizarConta).toHaveBeenCalledWith("conta-1");
    });
    await waitFor(() => {
      expect(mockPush).toHaveBeenCalledWith("/admin/alunos");
    });
  });

  it("erro ao anonimizar exibe alerta e permanece na página (sem navegar)", async () => {
    vi.mocked(adminApi.anonimizarConta).mockRejectedValue(new Error("falha"));
    await renderPage();
    fireEvent.click(screen.getByRole("tab", { name: "LGPD" }));

    fireEvent.click(screen.getByRole("button", { name: "Anonimizar conta (LGPD)" }));
    await waitFor(() => {
      expect(screen.getByRole("dialog")).toBeInTheDocument();
    });
    fireEvent.click(screen.getByRole("button", { name: "Anonimizar" }));

    await waitFor(() => {
      expect(screen.getByText("Erro ao anonimizar conta.")).toBeInTheDocument();
    });
    expect(mockPush).not.toHaveBeenCalled();
  });
});
