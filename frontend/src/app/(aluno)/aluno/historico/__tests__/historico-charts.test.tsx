import { describe, it, expect, afterEach, vi } from "vitest";
import { screen } from "@testing-library/react";
import { renderWithProviders } from "@/test/render";
import { FrequenciaChart, ProgressaoCargaChart } from "../_charts/HistoricoCharts";

vi.mock("recharts", () => ({
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  BarChart: ({ children }: { children: React.ReactNode }) => <svg role="img">{children}</svg>,
  Bar: () => null,
  LineChart: ({ children }: { children: React.ReactNode }) => <svg role="img">{children}</svg>,
  Line: () => null,
  XAxis: () => null,
  YAxis: () => null,
  CartesianGrid: () => null,
  Tooltip: () => null,
}));

afterEach(() => {
  vi.clearAllMocks();
});

describe("FrequenciaChart", () => {
  it("renderiza figure com aria-label de frequência semanal", () => {
    renderWithProviders(
      <FrequenciaChart weekData={[{ label: "Sem 1", sessoes: 2 }, { label: "Sem 2", sessoes: 3 }]} />,
      { skipAuth: true },
    );

    expect(screen.getByRole("figure", { name: "Frequência semanal de sessões" })).toBeInTheDocument();
  });

  it("expõe resumo textual srOnly com contagem de sessões por semana", () => {
    renderWithProviders(
      <FrequenciaChart weekData={[{ label: "Sem 1", sessoes: 2 }, { label: "Sem 2", sessoes: 3 }]} />,
      { skipAuth: true },
    );

    expect(screen.getByText("Sem 1: 2, Sem 2: 3")).toBeInTheDocument();
  });

  it("expõe resumo textual srOnly vazio quando não há dados de semana", () => {
    renderWithProviders(<FrequenciaChart weekData={[]} />, { skipAuth: true });

    const figure = screen.getByRole("figure", { name: "Frequência semanal de sessões" });
    expect(figure.querySelector("span")).toHaveTextContent("");
  });
});

describe("ProgressaoCargaChart", () => {
  it("interpola nomeExercicio no aria-label da figure", () => {
    renderWithProviders(
      <ProgressaoCargaChart
        nomeExercicio="Supino Reto"
        chartData={[{ data: "01/06", carga: 60, series: 3, reps: 10 }]}
      />,
      { skipAuth: true },
    );

    expect(screen.getByRole("figure", { name: "Progressão de carga — Supino Reto" })).toBeInTheDocument();
  });

  it("expõe resumo textual srOnly com carga por data", () => {
    renderWithProviders(
      <ProgressaoCargaChart
        nomeExercicio="Agachamento"
        chartData={[
          { data: "01/06", carga: 60, series: 3, reps: 10 },
          { data: "08/06", carga: 65, series: 3, reps: 8 },
        ]}
      />,
      { skipAuth: true },
    );

    expect(screen.getByText("01/06: 60 kg, 08/06: 65 kg")).toBeInTheDocument();
  });

  it("renderiza carga nula no resumo quando ponto não tem execução na semana", () => {
    renderWithProviders(
      <ProgressaoCargaChart
        nomeExercicio="Leg Press"
        chartData={[{ data: "01/06", carga: null, series: 0, reps: 0 }]}
      />,
      { skipAuth: true },
    );

    expect(screen.getByText("01/06: null kg")).toBeInTheDocument();
  });
});
