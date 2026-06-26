import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { renderWithProviders } from "@/test/render";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn() })),
}));

vi.mock("recharts", () => ({
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  LineChart: () => null, Line: () => null, XAxis: () => null, YAxis: () => null,
  CartesianGrid: () => null, Tooltip: () => null, BarChart: () => null, Bar: () => null,
}));

vi.mock("@/hooks/usePaginatedList", () => ({
  usePaginatedList: vi.fn(() => ({
    items: [], total: 0, page: 0, pageSize: 20, loading: false,
    error: "", success: "",
    setPage: vi.fn(), setPageSize: vi.fn(), setError: vi.fn(),
    setSuccess: vi.fn(), reload: vi.fn(),
  })),
}));

const dashboardOk = {
  totalFichas: 0,
  fichasAtivas: [],
  totalExecucoes: 0,
  sessoesPorSemana: [],
  vinculo: { ativo: true, pendente: false },
};

describe("HistoricoAlunoPage — erro nos indicadores", () => {
  beforeEach(() => {
    server.use(
      http.get("*/aluno/progressao", () => HttpResponse.json({ exercicios: [] })),
    );
  });

  it("agregado /aluno/dashboard falha → exibe AlertBanner", async () => {
    server.use(
      http.get("*/aluno/dashboard", () => new HttpResponse(null, { status: 500 })),
    );
    const { default: Page } = await import("@/app/(aluno)/aluno/historico/page");
    renderWithProviders(<Page />, { skipAuth: true });
    expect(
      await screen.findByText("Não foi possível carregar os indicadores do histórico."),
    ).toBeInTheDocument();
  });

  it("getMinhaProgressao falha → exibe erro na seção de progressão (não gráfico vazio)", async () => {
    server.use(
      http.get("*/aluno/dashboard", () => HttpResponse.json(dashboardOk)),
      http.get("*/aluno/progressao", () => new HttpResponse(null, { status: 500 })),
    );
    const { default: Page } = await import("@/app/(aluno)/aluno/historico/page");
    renderWithProviders(<Page />, { skipAuth: true });
    expect(
      await screen.findByText("Não foi possível carregar a progressão."),
    ).toBeInTheDocument();
    expect(screen.queryByText("Nenhuma execução registrada no período.")).not.toBeInTheDocument();
  });
});
