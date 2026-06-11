import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";

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

describe("HistoricoAlunoPage — erro nos indicadores", () => {
  beforeEach(() => {
    server.use(
      http.get("*/aluno/vinculo", () => HttpResponse.json({ vinculoAtivo: { vinculoId: "v" }, vinculoPendente: null })),
      http.get("*/aluno/progressao", () => HttpResponse.json({ exercicios: [] })),
    );
  });

  it("listExecucoes (indicadores) falha → exibe AlertBanner", async () => {
    server.use(
      http.get("*/aluno/execucoes", () => new HttpResponse(null, { status: 500 })),
    );
    const { default: Page } = await import("@/app/(aluno)/aluno/historico/page");
    render(<Page />);
    expect(
      await screen.findByText("Não foi possível carregar os indicadores do histórico."),
    ).toBeInTheDocument();
  });
});
