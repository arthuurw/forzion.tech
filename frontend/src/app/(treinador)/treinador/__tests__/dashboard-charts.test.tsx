import { describe, it, expect, afterEach, vi } from "vitest";
import { screen, within } from "@testing-library/react";
import { renderWithProviders } from "@/test/render";
import TreinadorDashboardCharts from "../_charts/TreinadorDashboardCharts";

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

const alunoStats = [
  { name: "Ativos", value: 5, color: "#00ff00" },
  { name: "Aguardando", value: 2, color: "#ffff00" },
];

describe("TreinadorDashboardCharts", () => {
  it("renderiza figure 'Alunos por status'", () => {
    renderWithProviders(
      <TreinadorDashboardCharts alunoStats={alunoStats} objetivoData={[]} receitaPorPacote={[]} />,
      { skipAuth: true },
    );

    expect(screen.getByRole("figure", { name: "Alunos por status" })).toBeInTheDocument();
  });

  it("objetivoData vazio → exibe 'Nenhuma ficha criada ainda.' sem figure de fichas por objetivo", () => {
    renderWithProviders(
      <TreinadorDashboardCharts alunoStats={alunoStats} objetivoData={[]} receitaPorPacote={[]} />,
      { skipAuth: true },
    );

    expect(screen.getByText("Nenhuma ficha criada ainda.")).toBeInTheDocument();
    expect(screen.queryByRole("figure", { name: "Fichas por objetivo" })).not.toBeInTheDocument();
  });

  it("objetivoData preenchido → renderiza figure 'Fichas por objetivo' em vez do texto vazio", () => {
    renderWithProviders(
      <TreinadorDashboardCharts
        alunoStats={alunoStats}
        objetivoData={[{ name: "Hipertrofia", total: 3 }]}
        receitaPorPacote={[]}
      />,
      { skipAuth: true },
    );

    expect(screen.getByRole("figure", { name: "Fichas por objetivo" })).toBeInTheDocument();
    expect(screen.queryByText("Nenhuma ficha criada ainda.")).not.toBeInTheDocument();
  });

  it("receitaPorPacote vazio → não renderiza figure 'Receita por pacote'", () => {
    renderWithProviders(
      <TreinadorDashboardCharts alunoStats={alunoStats} objetivoData={[]} receitaPorPacote={[]} />,
      { skipAuth: true },
    );

    expect(screen.queryByRole("figure", { name: "Receita por pacote" })).not.toBeInTheDocument();
  });

  it("receitaPorPacote preenchido → renderiza figure 'Receita por pacote' com resumo formatado em BRL pt-BR", () => {
    renderWithProviders(
      <TreinadorDashboardCharts
        alunoStats={alunoStats}
        objetivoData={[]}
        receitaPorPacote={[{ name: "Mensal", receita: 750, alunos: 5 }]}
      />,
      { skipAuth: true },
    );

    const figure = screen.getByRole("figure", { name: "Receita por pacote" });
    const formatado = (750).toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
    const esperado = `Mensal: ${formatado}`;
    expect(
      within(figure).getByText(
        (_, element) => element?.tagName.toLowerCase() === "span" && element.textContent === esperado,
      ),
    ).toBeInTheDocument();
  });
});
