import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
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
    items: [], total: 7, page: 0, pageSize: 20, loading: false,
    error: "", success: "",
    setPage: vi.fn(), setPageSize: vi.fn(), setError: vi.fn(),
    setSuccess: vi.fn(), reload: vi.fn(),
  })),
}));

const dashboardSemVinculo = {
  totalFichas: 0,
  fichasAtivas: [],
  totalExecucoes: 9,
  sessoesPorSemana: [
    { semanaInicio: "2026-06-16T00:00:00Z", semanaFim: "2026-06-22T00:00:00Z", total: 3 },
  ],
  vinculo: { ativo: false, pendente: false },
};

describe("HistoricoAlunoPage — indicadores via agregado", () => {
  beforeEach(() => {
    server.use(
      http.get("*/aluno/progressao", () => HttpResponse.json({ exercicios: [] })),
    );
  });

  it("banner recebe vínculo do agregado sem chamar GET /aluno/vinculo", async () => {
    let vinculoCalls = 0;
    server.use(
      http.get("*/aluno/dashboard", () => HttpResponse.json(dashboardSemVinculo)),
      http.get("*/aluno/vinculo", () => {
        vinculoCalls++;
        return HttpResponse.json({ vinculoAtivo: null, vinculoPendente: null });
      }),
    );

    const { default: Page } = await import("@/app/(aluno)/aluno/historico/page");
    renderWithProviders(<Page />, { skipAuth: true });

    expect(await screen.findByText(/não tem um vínculo ativo/)).toBeInTheDocument();
    expect(screen.getByText(/7 sess.* no total/)).toBeInTheDocument();

    await waitFor(() => expect(vinculoCalls).toBe(0));
  });
});
