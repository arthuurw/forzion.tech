import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import type { ProgressaoAlunoResponse } from "@/types";

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

vi.mock("@/lib/api/treinador", () => ({
  treinadorApi: {
    getProgressaoAluno: vi.fn(),
  },
}));

import { treinadorApi } from "@/lib/api/treinador";
const mockGetProgressao = vi.mocked(treinadorApi.getProgressaoAluno);

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
    {
      nomeExercicio: "Agachamento livre",
      grupoMuscular: "Pernas",
      historico: [
        { data: "2026-01-02", cargaMaxima: 80, seriesExecutadas: 4, repeticoesExecutadas: 8 },
      ],
    },
  ],
};

beforeEach(() => {
  mockGetProgressao.mockResolvedValue({ data: progressao } as Awaited<ReturnType<typeof treinadorApi.getProgressaoAluno>>);
});

describe("ProgressaoAluno — a11y charts", () => {
  it("gráfico de volume por grupamento tem figure com aria-label acessível", async () => {
    const { default: ProgressaoAluno } = await import("@/components/treinador/ProgressaoAluno");
    render(<ProgressaoAluno alunoId="a-1" />);

    await waitFor(() => {
      expect(
        screen.getByRole("figure", { name: "Volume por grupamento muscular" }),
      ).toBeInTheDocument();
    });
  });

  it("cada exercício tem figure 'Progressão de carga — <nome>' com aria-label", async () => {
    const { default: ProgressaoAluno } = await import("@/components/treinador/ProgressaoAluno");
    render(<ProgressaoAluno alunoId="a-1" />);

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
