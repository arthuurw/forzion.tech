import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { renderWithProviders } from "@/test/render";
import { buildTreinador, buildAluno } from "@/test/factories";
import type { AdminDashboardResponse } from "@/types";

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

const treinadorAlpha = buildTreinador({
  treinadorId: "t-alpha",
  nome: "Alpha",
  status: "Ativo",
  createdAt: "2020-01-01T00:00:00Z",
});

const treinadorBeta = buildTreinador({
  treinadorId: "t-beta",
  nome: "Beta",
  status: "Ativo",
  createdAt: "2025-12-01T00:00:00Z",
});

const treinadorPendente = buildTreinador({
  treinadorId: "t-pendente",
  nome: "Treinador Pendente",
  status: "AguardandoAprovacao",
  createdAt: "2025-06-01T00:00:00Z",
});

const alunoPendente = buildAluno({
  alunoId: "a-pendente",
  nome: "Aluno Pendente",
  status: "AguardandoAprovacao",
});

const dashboard: AdminDashboardResponse = {
  treinadores: { ativos: 42, pendentes: 7, inativos: 3 },
  alunos: { ativos: 99, pendentes: 5, inativos: 11 },
  totals: { planos: 2, exerciciosGlobais: 123, gruposMusculares: 17 },
  planoDistribuicao: [{ tier: "plano-basic-id", total: 8 }],
  alunoFinalidade: [{ finalidade: "Emagrecimento", total: 20 }],
  treinadoresPendentes: [treinadorPendente],
  alunosPendentes: [alunoPendente],
  recentTreinadores: [treinadorAlpha, treinadorBeta],
  planos: [
    { planoId: "plano-basic-id", nome: "Basic", tier: "Basic", preco: 49, maxAlunos: 20, descricao: null },
  ],
};

describe("DashboardAdminPage — agregado /admin/dashboard", () => {
  beforeEach(() => {
    server.use(
      http.get("*/admin/dashboard", () => HttpResponse.json(dashboard)),
    );
  });

  it("exibe counts de treinadores e alunos derivados do agregado", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await waitFor(() => {
      expect(screen.getByText("42")).toBeInTheDocument();
    });
    expect(screen.getByText("99")).toBeInTheDocument();
    expect(screen.getByText("7")).toBeInTheDocument();
  });

  it("exibe treinadores e alunos pendentes do agregado na aba Aprovações", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await waitFor(() => expect(screen.queryByRole("progressbar")).not.toBeInTheDocument());

    screen.getByRole("tab", { name: /Aprovações/ }).click();

    await waitFor(() => {
      expect(screen.getByText("Treinador Pendente")).toBeInTheDocument();
      expect(screen.getByText("Aluno Pendente")).toBeInTheDocument();
    });
  });

  it("renderiza recentTreinadores na ordem recebida do server sem re-sort client-side", async () => {
    // Alpha (createdAt 2020) is older but server places it first.
    // With createdAt-DESC re-sort still applied, Beta (2025) would appear first.
    const { default: Page } = await import("@/app/(admin)/admin/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await waitFor(() => expect(screen.queryByRole("progressbar")).not.toBeInTheDocument());

    screen.getByRole("tab", { name: "Plataforma" }).click();

    await waitFor(() => {
      expect(screen.getByText("Alpha")).toBeInTheDocument();
      expect(screen.getByText("Beta")).toBeInTheDocument();
    });

    const alphaEl = screen.getByText("Alpha");
    const betaEl = screen.getByText("Beta");
    expect(
      alphaEl.compareDocumentPosition(betaEl) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
  });

  it("faz apenas 1 GET /admin/dashboard — nenhum dos 11 endpoints antigos é chamado", async () => {
    let dashboardCalls = 0;
    let oldEndpointCalls = 0;

    server.use(
      http.get("*/admin/dashboard", () => {
        dashboardCalls++;
        return HttpResponse.json(dashboard);
      }),
      http.get("*/admin/treinadores", () => {
        oldEndpointCalls++;
        return HttpResponse.json({ items: [], total: 0, pagina: 1, tamanhoPagina: 1 });
      }),
      http.get("*/admin/alunos", () => {
        oldEndpointCalls++;
        return HttpResponse.json({ items: [], total: 0, pagina: 1, tamanhoPagina: 1 });
      }),
      http.get("*/admin/planos", () => {
        oldEndpointCalls++;
        return HttpResponse.json([]);
      }),
      http.get("*/admin/exercicios", () => {
        oldEndpointCalls++;
        return HttpResponse.json({ items: [], total: 0 });
      }),
      http.get("*/admin/grupos-musculares", () => {
        oldEndpointCalls++;
        return HttpResponse.json([]);
      }),
      http.get("*/admin/stats/dashboard", () => {
        oldEndpointCalls++;
        return HttpResponse.json({});
      }),
    );

    const { default: Page } = await import("@/app/(admin)/admin/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await waitFor(() => expect(dashboardCalls).toBe(1));
    expect(oldEndpointCalls).toBe(0);
  });
});
