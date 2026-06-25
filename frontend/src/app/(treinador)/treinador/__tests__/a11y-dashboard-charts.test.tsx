import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import { renderWithProviders } from "@/test/render";
import type { TreinadorDashboardResponse } from "@/types";

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
  XAxis: () => null,
  YAxis: () => null,
  Tooltip: () => null,
  Legend: () => null,
}));

vi.mock("@/lib/api/treinador", () => ({
  treinadorApi: {
    getDashboard: vi.fn(),
    aprovarVinculo: vi.fn(),
    desvincularAluno: vi.fn(),
  },
}));

import { treinadorApi } from "@/lib/api/treinador";

const dashboard: TreinadorDashboardResponse = {
  counts: { ativos: 5, aguardando: 2, inativos: 1 },
  mrr: 750,
  receitaPorPacote: [{ pacoteId: "pac-1", nome: "Mensal", alunos: 5, receita: 750 }],
  totalFichas: 3,
  objetivos: [{ objetivo: "Hipertrofia", total: 3 }],
  pendentes: [],
  onboarding: { onboardingCompleto: true, contaConfigurada: true, modoPagamentoAluno: "Plataforma", modoPagamentoPodeAlterarEm: null },
  plano: { status: "Ativa" },
};

describe("DashboardTreinadorPage — a11y charts", () => {
  beforeEach(() => {
    vi.mocked(treinadorApi.getDashboard).mockResolvedValue({ data: dashboard } as never);
  });

  it("figure 'Alunos por status' (PieChart) tem aria-label acessível", async () => {
    const { default: Page } = await import("@/app/(treinador)/treinador/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await waitFor(() => {
      expect(screen.getByRole("figure", { name: "Alunos por status" })).toBeInTheDocument();
    });
  });

  it("figure 'Fichas por objetivo' (BarChart) renderiza quando há fichas", async () => {
    const { default: Page } = await import("@/app/(treinador)/treinador/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await waitFor(() => {
      expect(screen.getByRole("figure", { name: "Fichas por objetivo" })).toBeInTheDocument();
    });
  });

  it("figure 'Receita por pacote' (BarChart) renderiza quando há receita de vínculos ativos", async () => {
    const { default: Page } = await import("@/app/(treinador)/treinador/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await waitFor(() => {
      expect(screen.getByRole("figure", { name: "Receita por pacote" })).toBeInTheDocument();
    });
  });
});
