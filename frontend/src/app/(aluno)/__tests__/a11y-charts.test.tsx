import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import { renderWithProviders } from "@/test/render";
import type { AlunoDashboardResponse } from "@/types";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn() })),
  useParams: vi.fn(() => ({})),
}));

vi.mock("recharts", () => ({
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  BarChart: ({ children }: { children: React.ReactNode }) => <svg role="img">{children}</svg>,
  Bar: () => null,
  XAxis: () => null,
  YAxis: () => null,
  Tooltip: () => null,
  Legend: () => null,
}));

vi.mock("@/lib/api/aluno", () => ({
  alunoApi: {
    getDashboard: vi.fn(),
  },
}));

import { alunoApi } from "@/lib/api/aluno";

const dashboard: AlunoDashboardResponse = {
  totalFichas: 2,
  fichasAtivas: [
    { treinoAlunoId: "ta-1", treinoId: "t-1", nomeTreino: "Treino A", objetivo: "Hipertrofia", criadoEm: "2026-01-01T00:00:00Z" },
  ],
  totalExecucoes: 5,
  sessoesPorSemana: [
    { semanaInicio: "2026-06-02T00:00:00Z", semanaFim: "2026-06-08T23:59:59Z", total: 3 },
    { semanaInicio: "2026-06-09T00:00:00Z", semanaFim: "2026-06-15T23:59:59Z", total: 0 },
    { semanaInicio: "2026-06-16T00:00:00Z", semanaFim: "2026-06-22T23:59:59Z", total: 2 },
    { semanaInicio: "2026-06-23T00:00:00Z", semanaFim: "2026-06-29T23:59:59Z", total: 0 },
    { semanaInicio: "2026-05-05T00:00:00Z", semanaFim: "2026-05-11T23:59:59Z", total: 0 },
    { semanaInicio: "2026-05-12T00:00:00Z", semanaFim: "2026-05-18T23:59:59Z", total: 0 },
    { semanaInicio: "2026-05-19T00:00:00Z", semanaFim: "2026-05-25T23:59:59Z", total: 0 },
    { semanaInicio: "2026-05-26T00:00:00Z", semanaFim: "2026-06-01T23:59:59Z", total: 0 },
  ],
  vinculo: { ativo: true, pendente: false },
};

describe("DashboardAlunoPage — a11y charts", () => {
  beforeEach(() => {
    vi.mocked(alunoApi.getDashboard).mockResolvedValue({
      data: dashboard,
    } as Awaited<ReturnType<typeof alunoApi.getDashboard>>);
  });

  it("gráfico de sessões por semana tem figure com aria-label acessível", async () => {
    const { default: Page } = await import("@/app/(aluno)/aluno/page");
    renderWithProviders(<Page />, { skipAuth: true });

    expect(await screen.findByRole("figure", { name: "Sessões por semana" })).toBeInTheDocument();
  });

  it("pie Fichas por status não é renderizado (R3b — dropar)", async () => {
    const { default: Page } = await import("@/app/(aluno)/aluno/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await screen.findByRole("figure", { name: "Sessões por semana" });
    expect(screen.queryByRole("figure", { name: "Fichas por status" })).not.toBeInTheDocument();
    expect(screen.queryByText(/Fichas por status/i)).not.toBeInTheDocument();
  });
});
