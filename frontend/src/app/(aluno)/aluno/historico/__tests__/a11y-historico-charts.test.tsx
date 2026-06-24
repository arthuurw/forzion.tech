import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import type { ExecucaoTreinoResponse, ExercicioProgressao, VinculoAlunoItemResponse } from "@/types";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn() })),
  useParams: vi.fn(() => ({})),
}));

vi.mock("recharts", () => ({
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  PieChart: ({ children }: { children: React.ReactNode }) => <svg role="img">{children}</svg>,
  Pie: () => null,
  Cell: () => null,
  BarChart: ({ children }: { children: React.ReactNode }) => <svg role="img">{children}</svg>,
  Bar: () => null,
  LineChart: ({ children }: { children: React.ReactNode }) => <svg role="img">{children}</svg>,
  Line: () => null,
  XAxis: () => null,
  YAxis: () => null,
  CartesianGrid: () => null,
  Tooltip: () => null,
  Legend: () => null,
}));

vi.mock("@/lib/api/aluno", () => ({
  alunoApi: {
    listExecucoes: vi.fn(),
    getMinhaProgressao: vi.fn(),
    getMeuVinculo: vi.fn(),
  },
}));

import { alunoApi } from "@/lib/api/aluno";

const vinculoAtivo: VinculoAlunoItemResponse = {
  vinculoId: "v-1",
  treinadorId: "tr-1",
  nomeTreinador: "Treinador X",
  status: "Ativo",
  dataInicio: new Date().toISOString(),
  createdAt: new Date().toISOString(),
};

const execucaoRecente: ExecucaoTreinoResponse = {
  execucaoId: "ex-1",
  treinoId: "t-1",
  alunoId: "a-1",
  dataExecucao: new Date().toISOString(),
  observacao: null,
  createdAt: new Date().toISOString(),
  nomeTreino: "Treino A",
  totalExercicios: 3,
  totalSeries: 9,
};

const exercicioProgressao: ExercicioProgressao = {
  nomeExercicio: "Supino Reto",
  grupoMuscular: "Peito",
  historico: [
    { data: new Date().toISOString(), cargaMaxima: 60, seriesExecutadas: 3, repeticoesExecutadas: 10 },
  ],
};

describe("HistoricoAlunoPage — a11y charts", () => {
  beforeEach(() => {
    vi.mocked(alunoApi.listExecucoes).mockResolvedValue({
      data: { items: [execucaoRecente], total: 1, pagina: 1, tamanhoPagina: 100 },
    } as Awaited<ReturnType<typeof alunoApi.listExecucoes>>);
    vi.mocked(alunoApi.getMinhaProgressao).mockResolvedValue({
      data: { exercicios: [exercicioProgressao] },
    } as Awaited<ReturnType<typeof alunoApi.getMinhaProgressao>>);
    vi.mocked(alunoApi.getMeuVinculo).mockResolvedValue({
      data: { vinculoAtivo, vinculoPendente: null },
    } as Awaited<ReturnType<typeof alunoApi.getMeuVinculo>>);
  });

  it("gráfico de frequência semanal tem figure com aria-label acessível", async () => {
    const { default: Page } = await import("@/app/(aluno)/aluno/historico/page");
    render(<Page />);

    await waitFor(() => {
      expect(
        screen.getByRole("figure", { name: "Frequência semanal de sessões" }),
      ).toBeInTheDocument();
    });
  });

  it("gráfico de progressão por exercício tem figure com aria-label acessível", async () => {
    const { default: Page } = await import("@/app/(aluno)/aluno/historico/page");
    render(<Page />);

    await waitFor(() => {
      expect(
        screen.getByRole("figure", { name: "Progressão de carga — Supino Reto" }),
      ).toBeInTheDocument();
    });
  });
});
