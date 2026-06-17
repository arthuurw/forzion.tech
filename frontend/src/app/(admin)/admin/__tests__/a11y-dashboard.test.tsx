/**
 * A11y + G-FE-3 tests for admin dashboard:
 *  1. Chart figures have aria-label (a11y — charts)
 *  2. IconButtons in treinadores page have aria-label (a11y — IconButtons)
 *  3. Admin dashboard consumes GET /admin/stats/dashboard endpoint (G-FE-3)
 *     instead of bulk listTreinadores/listAlunos for chart data.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type { TreinadorResponse } from "@/types";
import type { DashboardStatsResponse } from "@/lib/api/admin";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn() })),
  useParams: vi.fn(() => ({})),
}));

vi.mock("@/hooks/usePaginatedList", () => ({
  usePaginatedList: vi.fn(() => ({
    items: [],
    total: 0,
    page: 0,
    pageSize: 20,
    loading: false,
    error: "",
    success: "",
    setPage: vi.fn(),
    setPageSize: vi.fn(),
    setError: vi.fn(),
    setSuccess: vi.fn(),
    reload: vi.fn(),
  })),
}));

// Recharts renders SVG which jsdom doesn't measure well; stub containers
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

const dashboardStats: DashboardStatsResponse = {
  planoDistribuicao: [
    { tier: "plano-basic-id", total: 10 },
    { tier: "plano-pro-id", total: 5 },
  ],
  alunoFinalidade: [
    { finalidade: "Emagrecimento", total: 20 },
    { finalidade: "Hipertrofia", total: 15 },
  ],
};

function setupDefaultHandlers() {
  server.use(
    http.get("*/admin/treinadores", () => HttpResponse.json(emptyPaginated)),
    http.get("*/admin/alunos", () => HttpResponse.json(emptyPaginated)),
    http.get("*/admin/planos", () => HttpResponse.json([])),
    http.get("*/admin/exercicios", () => HttpResponse.json({ items: [], total: 0, pagina: 1, tamanhoPagina: 1 })),
    http.get("*/admin/grupos-musculares", () => HttpResponse.json([])),
    http.get("*/admin/stats/dashboard", () => HttpResponse.json(dashboardStats)),
  );
}

describe("DashboardAdminPage — G-FE-3 stats endpoint", () => {
  let statsCallCount = 0;
  let bulkTreinadoresCallCount = 0;

  beforeEach(() => {
    statsCallCount = 0;
    bulkTreinadoresCallCount = 0;

    server.use(
      http.get("*/admin/treinadores", ({ request }) => {
        const url = new URL(request.url);
        const tamanhoPagina = url.searchParams.get("tamanhoPagina");
        // Count bulk (large page) fetches — these should NOT happen anymore for chart data
        if (tamanhoPagina && parseInt(tamanhoPagina) > 20) {
          bulkTreinadoresCallCount++;
        }
        return HttpResponse.json(emptyPaginated);
      }),
      http.get("*/admin/alunos", () => HttpResponse.json(emptyPaginated)),
      http.get("*/admin/planos", () => HttpResponse.json([])),
      http.get("*/admin/exercicios", () => HttpResponse.json({ items: [], total: 0, pagina: 1, tamanhoPagina: 1 })),
      http.get("*/admin/grupos-musculares", () => HttpResponse.json([])),
      http.get("*/admin/stats/dashboard", () => {
        statsCallCount++;
        return HttpResponse.json(dashboardStats);
      }),
    );
  });

  it("chama GET /admin/stats/dashboard exatamente uma vez", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/page");
    render(<Page />);

    await waitFor(() => {
      expect(statsCallCount).toBe(1);
    });
  });

  it("não faz chamadas bulk (tamanhoPagina > 20) para gerar distribuição dos gráficos", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/page");
    render(<Page />);

    await waitFor(() => {
      expect(statsCallCount).toBe(1);
    });

    expect(bulkTreinadoresCallCount).toBe(0);
  });
});

describe("DashboardAdminPage — a11y charts", () => {
  beforeEach(() => {
    setupDefaultHandlers();
  });

  it("gráfico de status dos treinadores tem figure com aria-label acessível", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/page");
    render(<Page />);

    await waitFor(() => {
      expect(
        screen.getByRole("figure", { name: "Distribuição de treinadores por status" }),
      ).toBeInTheDocument();
    });
  });

  it("gráfico de treinadores por plano tem figure com aria-label acessível", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/page");
    render(<Page />);

    await waitFor(() => {
      expect(
        screen.getByRole("figure", { name: "Distribuição de treinadores por plano" }),
      ).toBeInTheDocument();
    });
  });

  it("gráfico de status dos alunos tem figure com aria-label acessível", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/page");
    render(<Page />);

    await waitFor(() => {
      expect(
        screen.getByRole("figure", { name: "Distribuição de alunos por status" }),
      ).toBeInTheDocument();
    });
  });

  it("gráfico de finalidade dos alunos tem figure com aria-label (quando há dados)", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/page");
    render(<Page />);

    await waitFor(() => {
      expect(
        screen.getByRole("figure", { name: "Distribuição de alunos por finalidade" }),
      ).toBeInTheDocument();
    });
  });
});

import { usePaginatedList } from "@/hooks/usePaginatedList";
const mockUsePaginatedList = vi.mocked(usePaginatedList);

describe("TreinadoresAdminPage — a11y IconButtons com aria-label", () => {
  const treinadorAguardando: TreinadorResponse = {
    treinadorId: "t-aguardando",
    nome: "Treinador Pendente",
    contaId: "c-1",
    status: "AguardandoAprovacao",
    planoPlataformaId: null,
    createdAt: "2025-01-01T00:00:00Z",
  };

  const treinadorAtivo: TreinadorResponse = {
    treinadorId: "t-ativo",
    nome: "Treinador Ativo",
    contaId: "c-2",
    status: "Ativo",
    planoPlataformaId: null,
    createdAt: "2025-01-01T00:00:00Z",
  };

  const treinadorInativo: TreinadorResponse = {
    treinadorId: "t-inativo",
    nome: "Treinador Inativo",
    contaId: "c-3",
    status: "Inativo",
    planoPlataformaId: null,
    createdAt: "2025-01-01T00:00:00Z",
  };

  beforeEach(() => {
    server.use(
      http.get("*/admin/planos", () => HttpResponse.json([])),
    );
  });

  it("AguardandoAprovacao → botões 'Aprovar treinador' e 'Reprovar treinador' têm aria-label", async () => {
    mockUsePaginatedList.mockReturnValue({
      items: [treinadorAguardando],
      total: 1,
      page: 0,
      pageSize: 20,
      loading: false,
      error: "",
      success: "",
      setPage: vi.fn(),
      setPageSize: vi.fn(),
      setError: vi.fn(),
      setSuccess: vi.fn(),
      reload: vi.fn(),
    });

    const { default: Page } = await import("@/app/(admin)/admin/treinadores/page");
    render(<Page />);

    expect(screen.getByRole("button", { name: "Aprovar treinador" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Reprovar treinador" })).toBeInTheDocument();
  });

  it("Ativo → botão 'Inativar treinador' tem aria-label", async () => {
    mockUsePaginatedList.mockReturnValue({
      items: [treinadorAtivo],
      total: 1,
      page: 0,
      pageSize: 20,
      loading: false,
      error: "",
      success: "",
      setPage: vi.fn(),
      setPageSize: vi.fn(),
      setError: vi.fn(),
      setSuccess: vi.fn(),
      reload: vi.fn(),
    });

    const { default: Page } = await import("@/app/(admin)/admin/treinadores/page");
    render(<Page />);

    expect(screen.getByRole("button", { name: "Inativar treinador" })).toBeInTheDocument();
  });

  it("Inativo → botão 'Excluir treinador permanentemente' tem aria-label", async () => {
    mockUsePaginatedList.mockReturnValue({
      items: [treinadorInativo],
      total: 1,
      page: 0,
      pageSize: 20,
      loading: false,
      error: "",
      success: "",
      setPage: vi.fn(),
      setPageSize: vi.fn(),
      setError: vi.fn(),
      setSuccess: vi.fn(),
      reload: vi.fn(),
    });

    const { default: Page } = await import("@/app/(admin)/admin/treinadores/page");
    render(<Page />);

    expect(screen.getByRole("button", { name: "Excluir treinador permanentemente" })).toBeInTheDocument();
  });

  it("sempre exibe botões 'Atribuir plano' e 'Ver detalhe do treinador' com aria-label", async () => {
    mockUsePaginatedList.mockReturnValue({
      items: [treinadorAtivo],
      total: 1,
      page: 0,
      pageSize: 20,
      loading: false,
      error: "",
      success: "",
      setPage: vi.fn(),
      setPageSize: vi.fn(),
      setError: vi.fn(),
      setSuccess: vi.fn(),
      reload: vi.fn(),
    });

    const { default: Page } = await import("@/app/(admin)/admin/treinadores/page");
    render(<Page />);

    expect(screen.getByRole("button", { name: "Atribuir plano" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Ver detalhe do treinador" })).toBeInTheDocument();
  });
});
