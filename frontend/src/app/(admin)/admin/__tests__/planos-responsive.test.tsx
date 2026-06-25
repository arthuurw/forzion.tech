import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { renderWithProviders } from "@/test/render";
import { useMediaQuery } from "@mui/material";
import type { AdminDashboardResponse } from "@/types";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn() })),
  useParams: vi.fn(() => ({})),
}));

vi.mock("@mui/material", async () => {
  const actual = await vi.importActual("@mui/material");
  return { ...(actual as object), useMediaQuery: vi.fn(() => false) };
});

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

const adminDashboard: AdminDashboardResponse = {
  treinadores: { ativos: 0, pendentes: 0, inativos: 0 },
  alunos: { ativos: 0, pendentes: 0, inativos: 0 },
  totals: { planos: 2, exerciciosGlobais: 0, gruposMusculares: 0 },
  planoDistribuicao: [
    { tier: "plano-pro-id", total: 7 },
    { tier: "__none", total: 3 },
  ],
  alunoFinalidade: [],
  treinadoresPendentes: [],
  alunosPendentes: [],
  recentTreinadores: [],
  planos: [
    { planoId: "plano-pro-id", nome: "Pro", tier: "Pro", preco: 99, maxAlunos: 50, descricao: null },
  ],
};

function setupHandlers() {
  server.use(
    http.get("*/admin/dashboard", () => HttpResponse.json(adminDashboard)),
  );
}

async function renderOnPlataformaTab() {
  const { default: Page } = await import("@/app/(admin)/admin/page");
  renderWithProviders(<Page />, { skipAuth: true });

  const tab = await screen.findByRole("tab", { name: "Plataforma" });
  tab.click();
}

describe("DashboardAdminPage — tabela de planos responsiva (cards em <md)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    setupHandlers();
    vi.mocked(useMediaQuery).mockReturnValue(true);
  });

  it("em mobile a seção de planos não renderiza <table> e mostra os dados como cards", async () => {
    await renderOnPlataformaTab();

    const heading = await screen.findByText("PLANOS DE TREINADORES");
    const section = heading.closest("div");
    expect(section).not.toBeNull();

    await waitFor(() => {
      expect(within(section as HTMLElement).getByText("Pro")).toBeInTheDocument();
    });

    expect(within(section as HTMLElement).queryByRole("table")).not.toBeInTheDocument();
  });

  it("o plano real, a linha 'Sem plano atribuído' e os Chips de contagem aparecem como conteúdo de card", async () => {
    await renderOnPlataformaTab();

    await waitFor(() => {
      expect(screen.getByText("Pro")).toBeInTheDocument();
    });

    expect(screen.getByText("Sem plano atribuído")).toBeInTheDocument();
    expect(screen.getByText("7")).toBeInTheDocument();
    expect(screen.getByText("3")).toBeInTheDocument();
  });
});
