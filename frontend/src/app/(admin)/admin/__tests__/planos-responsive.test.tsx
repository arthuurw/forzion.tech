import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { renderWithProviders } from "@/test/render";
import { useMediaQuery } from "@mui/material";
import type { DashboardStatsResponse } from "@/lib/api/admin";

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

const emptyPaginated = { items: [], total: 0, pagina: 1, tamanhoPagina: 1 };

const planos = [
  { planoId: "plano-pro-id", nome: "Pro", tier: "Pro", preco: 99, maxAlunos: 50, descricao: null },
];

const dashboardStats: DashboardStatsResponse = {
  planoDistribuicao: [
    { tier: "plano-pro-id", total: 7 },
    { tier: "__none", total: 3 },
  ],
  alunoFinalidade: [],
};

function setupHandlers() {
  server.use(
    http.get("*/admin/treinadores", () => HttpResponse.json(emptyPaginated)),
    http.get("*/admin/alunos", () => HttpResponse.json(emptyPaginated)),
    http.get("*/admin/planos", () => HttpResponse.json(planos)),
    http.get("*/admin/exercicios", () => HttpResponse.json({ items: [], total: 0, pagina: 1, tamanhoPagina: 1 })),
    http.get("*/admin/grupos-musculares", () => HttpResponse.json([])),
    http.get("*/admin/stats/dashboard", () => HttpResponse.json(dashboardStats)),
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
