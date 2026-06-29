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

const baseDashboard: TreinadorDashboardResponse = {
  counts: { ativos: 5, aguardando: 2, inativos: 1 },
  mrr: 750,
  receitaPorPacote: [{ pacoteId: "pac-1", nome: "Mensal", alunos: 5, receita: 750 }],
  totalFichas: 3,
  objetivos: [{ objetivo: "Hipertrofia", total: 3 }],
  pendentes: [],
  onboarding: { onboardingCompleto: true, contaConfigurada: true, modoPagamentoAluno: "Plataforma", modoPagamentoPodeAlterarEm: null },
  plano: { status: "Ativa" },
  dadosFiscaisPendentes: false,
};

async function renderPage() {
  const { default: Page } = await import("@/app/(treinador)/treinador/page");
  renderWithProviders(<Page />, { skipAuth: true });
}

describe("DashboardTreinadorPage — banner dados fiscais", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renderiza o banner com link quando dadosFiscaisPendentes é true", async () => {
    vi.mocked(treinadorApi.getDashboard).mockResolvedValue({
      data: { ...baseDashboard, dadosFiscaisPendentes: true },
    } as never);

    await renderPage();

    const banner = await screen.findByText("Complete seus dados fiscais");
    expect(banner).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /completar dados fiscais/i })).toBeInTheDocument();
  });

  it("não renderiza o banner quando dadosFiscaisPendentes é false", async () => {
    vi.mocked(treinadorApi.getDashboard).mockResolvedValue({
      data: { ...baseDashboard, dadosFiscaisPendentes: false },
    } as never);

    await renderPage();

    await screen.findByText("Ativos");
    expect(screen.queryByText("Complete seus dados fiscais")).not.toBeInTheDocument();
  });
});
