import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import type {
  AlunoResponse, MeuVinculoResponse, ProgressaoAlunoResponse, PaginatedResponse,
  FichaAlunoResponse, ExecucaoTreinoResponse,
} from "@/types";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn() })),
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
  },
}));

import { adminApi } from "@/lib/api/admin";

const aluno: AlunoResponse = {
  alunoId: "a-1",
  nome: "Maria Aluna",
  email: "maria@example.com",
  telefone: null,
  status: "Ativo",
  contaId: "c-1",
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: null,
  diasDisponiveis: null,
  tempoDisponivelMinutos: null,
  finalidade: null,
  focoTreino: null,
  nivelCondicionamento: null,
  limitacoesFisicas: null,
} as AlunoResponse;

const vinculo: MeuVinculoResponse = { vinculoAtivo: null, vinculoPendente: null };

const emptyPaginated: PaginatedResponse<FichaAlunoResponse> = { items: [], total: 0, pagina: 1, tamanhoPagina: 20 };
const emptyExecucoes: PaginatedResponse<ExecucaoTreinoResponse> = { items: [], total: 0, pagina: 1, tamanhoPagina: 20 };

const progressao: ProgressaoAlunoResponse = {
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

beforeEach(() => {
  vi.mocked(adminApi.getAluno).mockResolvedValue({ data: aluno } as Awaited<ReturnType<typeof adminApi.getAluno>>);
  vi.mocked(adminApi.getAlunoVinculo).mockResolvedValue({ data: vinculo } as Awaited<ReturnType<typeof adminApi.getAlunoVinculo>>);
  vi.mocked(adminApi.getAlunoFichas).mockResolvedValue({ data: emptyPaginated } as Awaited<ReturnType<typeof adminApi.getAlunoFichas>>);
  vi.mocked(adminApi.getAlunoExecucoes).mockResolvedValue({ data: emptyExecucoes } as Awaited<ReturnType<typeof adminApi.getAlunoExecucoes>>);
  vi.mocked(adminApi.getAlunoProgressao).mockResolvedValue({ data: progressao } as Awaited<ReturnType<typeof adminApi.getAlunoProgressao>>);
});

describe("DetalheAlunoAdminPage — a11y charts (aba Progressão)", () => {
  it("cada exercício tem figure 'Progressão de carga — <nome>' com aria-label", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/alunos/[alunoId]/page");
    render(<Page />);

    await waitFor(() => {
      expect(screen.getByRole("tab", { name: "Progressão" })).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole("tab", { name: "Progressão" }));

    await waitFor(() => {
      const figures = screen.getAllByRole("figure");
      expect(
        figures.some((f) => f.getAttribute("aria-label")?.startsWith("Progressão de carga — ")),
      ).toBe(true);
    });

    expect(
      screen.getByRole("figure", { name: "Progressão de carga — Supino reto" }),
    ).toBeInTheDocument();
  });
});
