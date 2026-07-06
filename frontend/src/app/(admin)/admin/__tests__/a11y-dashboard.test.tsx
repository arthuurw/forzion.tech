import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { renderWithProviders } from "@/test/render";
import type { TreinadorResponse, AdminDashboardResponse } from "@/types";

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
  treinadores: { ativos: 5, pendentes: 2, inativos: 1 },
  alunos: { ativos: 20, pendentes: 3, inativos: 2 },
  totals: { planos: 2, exerciciosGlobais: 50, gruposMusculares: 6 },
  planoDistribuicao: [
    { tier: "plano-basic-id", total: 10 },
    { tier: "plano-pro-id", total: 5 },
  ],
  alunoFinalidade: [
    { finalidade: "Emagrecimento", total: 20 },
    { finalidade: "Hipertrofia", total: 15 },
  ],
  treinadoresPendentes: [],
  alunosPendentes: [],
  recentTreinadores: [],
  planos: [],
};

function setupDefaultHandlers() {
  server.use(
    http.get("*/admin/dashboard", () => HttpResponse.json(adminDashboard)),
  );
}

describe("DashboardAdminPage — single aggregate endpoint", () => {
  let dashboardCallCount = 0;
  let oldEndpointCallCount = 0;

  beforeEach(() => {
    dashboardCallCount = 0;
    oldEndpointCallCount = 0;

    server.use(
      http.get("*/admin/dashboard", () => {
        dashboardCallCount++;
        return HttpResponse.json(adminDashboard);
      }),
      http.get("*/admin/treinadores", () => {
        oldEndpointCallCount++;
        return HttpResponse.json({ items: [], total: 0, pagina: 1, tamanhoPagina: 1 });
      }),
      http.get("*/admin/alunos", () => {
        oldEndpointCallCount++;
        return HttpResponse.json({ items: [], total: 0, pagina: 1, tamanhoPagina: 1 });
      }),
      http.get("*/admin/planos", () => {
        oldEndpointCallCount++;
        return HttpResponse.json([]);
      }),
      http.get("*/admin/exercicios", () => {
        oldEndpointCallCount++;
        return HttpResponse.json({ items: [], total: 0, pagina: 1, tamanhoPagina: 1 });
      }),
      http.get("*/admin/grupos-musculares", () => {
        oldEndpointCallCount++;
        return HttpResponse.json([]);
      }),
      http.get("*/admin/stats/dashboard", () => {
        oldEndpointCallCount++;
        return HttpResponse.json({});
      }),
    );
  });

  it("chama GET /admin/dashboard exatamente uma vez", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await waitFor(() => {
      expect(dashboardCallCount).toBe(1);
    });
  });

  it("não faz chamadas aos 11 endpoints antigos do burst", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await waitFor(() => {
      expect(dashboardCallCount).toBe(1);
    });

    expect(oldEndpointCallCount).toBe(0);
  });
});

describe("DashboardAdminPage — a11y charts", () => {
  beforeEach(() => {
    setupDefaultHandlers();
  });

  it("gráfico de status dos treinadores tem figure com aria-label acessível", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await waitFor(() => {
      expect(
        screen.getByRole("figure", { name: "Distribuição de treinadores por status" }),
      ).toBeInTheDocument();
    });
  });

  it("gráfico de treinadores por plano tem figure com aria-label acessível", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await waitFor(() => {
      expect(
        screen.getByRole("figure", { name: "Distribuição de treinadores por plano" }),
      ).toBeInTheDocument();
    });
  });

  it("gráfico de status dos alunos tem figure com aria-label acessível", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await waitFor(() => {
      expect(
        screen.getByRole("figure", { name: "Distribuição de alunos por status" }),
      ).toBeInTheDocument();
    });
  });

  it("gráfico de finalidade dos alunos tem figure com aria-label (quando há dados)", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/page");
    renderWithProviders(<Page />, { skipAuth: true });

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
    planoCortesiaId: null,
    createdAt: "2025-01-01T00:00:00Z",
  };

  const treinadorAtivo: TreinadorResponse = {
    treinadorId: "t-ativo",
    nome: "Treinador Ativo",
    contaId: "c-2",
    status: "Ativo",
    planoPlataformaId: null,
    planoCortesiaId: null,
    createdAt: "2025-01-01T00:00:00Z",
  };

  const treinadorInativo: TreinadorResponse = {
    treinadorId: "t-inativo",
    nome: "Treinador Inativo",
    contaId: "c-3",
    status: "Inativo",
    planoPlataformaId: null,
    planoCortesiaId: null,
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
    renderWithProviders(<Page />, { skipAuth: true });

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
    renderWithProviders(<Page />, { skipAuth: true });

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
    renderWithProviders(<Page />, { skipAuth: true });

    expect(screen.getByRole("button", { name: "Excluir treinador permanentemente" })).toBeInTheDocument();
  });

  it("sempre exibe botões 'Conceder cortesia' e 'Ver detalhe do treinador' com aria-label", async () => {
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
    renderWithProviders(<Page />, { skipAuth: true });

    expect(screen.getByRole("button", { name: "Conceder cortesia" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Ver detalhe do treinador" })).toBeInTheDocument();
  });
});
