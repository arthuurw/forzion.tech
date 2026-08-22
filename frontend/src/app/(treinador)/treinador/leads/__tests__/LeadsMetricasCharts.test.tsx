import { describe, it, expect, afterEach, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import LeadsMetricasCharts from "../_charts/LeadsMetricasCharts";
import type { ObterMetricasLeadsResponse } from "@/types";

vi.mock("recharts", () => ({
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  PieChart: ({ children }: { children: React.ReactNode }) => <svg role="img">{children}</svg>,
  Pie: () => null,
  Cell: () => null,
  BarChart: ({ children }: { children: React.ReactNode }) => <svg role="img">{children}</svg>,
  Bar: () => null,
  XAxis: () => null,
  YAxis: () => null,
  Tooltip: () => null,
  Legend: () => null,
}));

afterEach(() => {
  vi.clearAllMocks();
});

function metricas(overrides: Partial<ObterMetricasLeadsResponse> = {}): ObterMetricasLeadsResponse {
  return {
    total: 10,
    porOrigemAgente: 7,
    porOrigemManual: 3,
    convertidos: 5,
    taxaConversao: 0.5,
    porMotivoDescarte: { SemInteresse: 2, Duplicado: 1 },
    ...overrides,
  };
}

describe("LeadsMetricasCharts", () => {
  it("exibe os cards de total, origem, convertidos e taxa de conversão", () => {
    render(<LeadsMetricasCharts metricas={metricas()} />);
    expect(screen.getByText("10")).toBeInTheDocument();
    expect(screen.getByText("7")).toBeInTheDocument();
    expect(screen.getByText("5")).toBeInTheDocument();
    expect(screen.getByText("50.0%")).toBeInTheDocument();
  });

  it("exibe a quebra de motivos de descarte", () => {
    render(<LeadsMetricasCharts metricas={metricas()} />);
    expect(screen.getByText("Sem interesse: 2, Duplicado: 1")).toBeInTheDocument();
  });

  it("período sem leads exibe zeros explícitos sem quebrar", () => {
    render(<LeadsMetricasCharts metricas={metricas({
      total: 0, porOrigemAgente: 0, porOrigemManual: 0, convertidos: 0, taxaConversao: 0, porMotivoDescarte: {},
    })} />);
    expect(screen.getAllByText("0").length).toBeGreaterThan(0);
    expect(screen.getByText("0.0%")).toBeInTheDocument();
    expect(screen.queryByText(/motivos de descarte/i)).not.toBeInTheDocument();
  });

  it("sem descartes no período, mas com leads, informa que não há descarte", () => {
    render(<LeadsMetricasCharts metricas={metricas({ porMotivoDescarte: {} })} />);
    expect(screen.getByText("Nenhum lead descartado no período.")).toBeInTheDocument();
  });
});
