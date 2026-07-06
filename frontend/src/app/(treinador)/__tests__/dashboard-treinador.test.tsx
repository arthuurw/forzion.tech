import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor, fireEvent } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { renderWithProviders } from "@/test/render";
import type { VinculoDetalheResponse, TreinadorDashboardResponse } from "@/types";

const mockPush = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: mockPush, back: vi.fn(), replace: vi.fn() })),
  useParams: vi.fn(() => ({})),
}));

vi.mock("recharts", () => ({
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  PieChart: () => null,
  Pie: () => null,
  Cell: () => null,
  BarChart: () => null,
  Bar: () => null,
  XAxis: () => null,
  YAxis: () => null,
  Tooltip: () => null,
  Legend: () => null,
}));

function buildVinculo(overrides: Partial<VinculoDetalheResponse> = {}): VinculoDetalheResponse {
  return {
    vinculoId: "v-1",
    treinadorId: "t-1",
    alunoId: "a-1",
    nomeAluno: "João Aluno",
    emailAluno: "joao@test.com",
    status: "AguardandoAprovacao",
    createdAt: "2025-01-01T00:00:00Z",
    pacoteId: "pac-1",
    temVinculoAtivoPrevio: false,
    preservarNoLimite: false,
    ...overrides,
  };
}

const PLANO_ATIVO_BASE = {
  status: "Ativa" as const,
  tierEfetivo: "Pro" as const,
  planoContratadoId: null,
  alunosAtivos: 5,
  capEfetivo: 30,
  excedente: 0,
  gracaAte: null,
  temCortesia: false,
};

function buildDashboard(overrides: Partial<TreinadorDashboardResponse> = {}): TreinadorDashboardResponse {
  return {
    counts: { ativos: 5, aguardando: 2, inativos: 1 },
    mrr: 750,
    receitaPorPacote: [{ pacoteId: "pac-1", nome: "Plano Bronze", alunos: 5, receita: 750 }],
    totalFichas: 10,
    objetivos: [{ objetivo: "Hipertrofia", total: 7 }, { objetivo: "Emagrecimento", total: 3 }],
    pendentes: [],
    onboarding: { onboardingCompleto: true, contaConfigurada: true, modoPagamentoAluno: "Plataforma", modoPagamentoPodeAlterarEm: null },
    plano: PLANO_ATIVO_BASE,
    dadosFiscaisPendentes: false,
    ...overrides,
  };
}

describe("DashboardTreinadorPage — null pacoteId redirect (G-FE-2)", () => {
  beforeEach(() => {
    mockPush.mockClear();
  });

  it("Aprovar button is disabled when pacoteId is null and no redirect fires on render", async () => {
    const vinculoSemPacote = buildVinculo({ pacoteId: null });
    server.use(
      http.get("*/treinador/dashboard", () =>
        HttpResponse.json(buildDashboard({ pendentes: [vinculoSemPacote] })),
      ),
    );

    const { default: Page } = await import("@/app/(treinador)/treinador/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await waitFor(() => {
      expect(screen.queryByRole("progressbar")).not.toBeInTheDocument();
    });

    const buttons = screen.getAllByRole("button", { name: /aprovar/i });
    expect(buttons.length).toBeGreaterThan(0);
    expect(mockPush).not.toHaveBeenCalledWith("/treinador/alunos");
    expect(buttons[0]).toBeDisabled();
  });

  it("Aprovar button is enabled and no redirect fires when pacoteId is set", async () => {
    const vinculoComPacote = buildVinculo({ pacoteId: "pac-1" });
    server.use(
      http.get("*/treinador/dashboard", () =>
        HttpResponse.json(buildDashboard({ pendentes: [vinculoComPacote] })),
      ),
      http.post("*/treinador/vinculos/:id/aprovar", () => HttpResponse.json({})),
    );

    const { default: Page } = await import("@/app/(treinador)/treinador/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await waitFor(() => {
      expect(screen.queryByRole("progressbar")).not.toBeInTheDocument();
    });

    const buttons = screen.getAllByRole("button", { name: /aprovar/i });
    expect(buttons.length).toBeGreaterThan(0);
    expect(buttons[0]).toBeEnabled();

    fireEvent.click(buttons[0]);

    await waitFor(() => {
      expect(mockPush).not.toHaveBeenCalledWith("/treinador/alunos");
    });
  });
});

describe("DashboardTreinadorPage — banners onboarding e inadimplente", () => {
  it("modo Externo não exibe o banner de onboarding", async () => {
    server.use(
      http.get("*/treinador/dashboard", () =>
        HttpResponse.json(buildDashboard({
          onboarding: { onboardingCompleto: false, contaConfigurada: false, modoPagamentoAluno: "Externo", modoPagamentoPodeAlterarEm: null },
        })),
      ),
    );

    const { default: Page } = await import("@/app/(treinador)/treinador/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await waitFor(() => expect(screen.queryByRole("progressbar")).not.toBeInTheDocument());
    expect(screen.queryByText("Configure seus recebimentos")).not.toBeInTheDocument();
  });

  it("modo Plataforma com onboarding pendente exibe o banner de onboarding", async () => {
    server.use(
      http.get("*/treinador/dashboard", () =>
        HttpResponse.json(buildDashboard({
          onboarding: { onboardingCompleto: false, contaConfigurada: false, modoPagamentoAluno: "Plataforma", modoPagamentoPodeAlterarEm: null },
        })),
      ),
    );

    const { default: Page } = await import("@/app/(treinador)/treinador/page");
    renderWithProviders(<Page />, { skipAuth: true });

    expect(await screen.findByText("Configure seus recebimentos")).toBeInTheDocument();
  });

  it("plano inadimplente exibe o banner de regularização", async () => {
    server.use(
      http.get("*/treinador/dashboard", () =>
        HttpResponse.json(buildDashboard({ plano: { ...PLANO_ATIVO_BASE, status: "Inadimplente" } })),
      ),
    );

    const { default: Page } = await import("@/app/(treinador)/treinador/page");
    renderWithProviders(<Page />, { skipAuth: true });

    expect(await screen.findByText("Assinatura da plataforma em atraso")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Regularizar pagamento/ })).toBeInTheDocument();
  });
});

describe("DashboardTreinadorPage — load error shows backend detail (G-FE-1)", () => {
  it("shows backend detail message from ProblemDetails when load fails", async () => {
    server.use(
      http.get("*/treinador/dashboard", () =>
        HttpResponse.json(
          { detail: "Treinador não encontrado.", title: "Not Found" },
          { status: 404 },
        ),
      ),
    );

    const { default: Page } = await import("@/app/(treinador)/treinador/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await waitFor(() => {
      expect(screen.getByText("Treinador não encontrado.")).toBeInTheDocument();
    });
  });
});

describe("DashboardTreinadorPage — agregado /treinador/dashboard (T5)", () => {
  const dashboard: TreinadorDashboardResponse = {
    counts: { ativos: 12, aguardando: 3, inativos: 2 },
    mrr: 1800,
    receitaPorPacote: [{ pacoteId: "pac-1", nome: "Plano Bronze", alunos: 12, receita: 1800 }],
    totalFichas: 25,
    objetivos: [
      { objetivo: "Hipertrofia", total: 15 },
      { objetivo: "Emagrecimento", total: 10 },
    ],
    pendentes: [],
    onboarding: { onboardingCompleto: false, contaConfigurada: false, modoPagamentoAluno: "Plataforma", modoPagamentoPodeAlterarEm: null },
    plano: { ...PLANO_ATIVO_BASE, status: "Inadimplente" },
    dadosFiscaisPendentes: false,
  };

  beforeEach(() => {
    server.use(
      http.get("*/treinador/dashboard", () => HttpResponse.json(dashboard)),
    );
  });

  it("renderiza counts, totalFichas, banners de onboarding e inadimplente do agregado", async () => {
    const { default: Page } = await import("@/app/(treinador)/treinador/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await waitFor(() => {
      expect(screen.getByText("12")).toBeInTheDocument();
    });
    expect(screen.getByText("3")).toBeInTheDocument();
    expect(screen.getByText("25")).toBeInTheDocument();
    expect(screen.getByText("Assinatura da plataforma em atraso")).toBeInTheDocument();
    expect(screen.getByText("Configure seus recebimentos")).toBeInTheDocument();
  });

  it("exibe MRR igual ao valor servidor sem re-somar lista paginada (R2b)", async () => {
    const { default: Page } = await import("@/app/(treinador)/treinador/page");
    renderWithProviders(<Page />, { skipAuth: true });

    expect(await screen.findByText("12")).toBeInTheDocument();
    expect(document.body).toHaveTextContent(/1[.,]800/);
  });

  it("faz apenas 1 GET /treinador/dashboard — os 7 endpoints antigos não são chamados", async () => {
    let dashboardCalls = 0;
    let oldEndpointCalls = 0;

    server.use(
      http.get("*/treinador/dashboard", () => {
        dashboardCalls++;
        return HttpResponse.json(dashboard);
      }),
      http.get("*/treinador/vinculos", () => {
        oldEndpointCalls++;
        return HttpResponse.json({ items: [], total: 0, pagina: 1, tamanhoPagina: 1 });
      }),
      http.get("*/treinador/treinos", () => {
        oldEndpointCalls++;
        return HttpResponse.json({ items: [], total: 0, pagina: 1, tamanhoPagina: 100 });
      }),
      http.get("*/treinador/pacotes", () => {
        oldEndpointCalls++;
        return HttpResponse.json([]);
      }),
      http.get("*/treinador/onboarding/status", () => {
        oldEndpointCalls++;
        return HttpResponse.json({ onboardingCompleto: true, contaConfigurada: true });
      }),
      http.get("*/treinador/plano/assinatura", () => {
        oldEndpointCalls++;
        return HttpResponse.json({ status: "Ativa" });
      }),
    );

    const { default: Page } = await import("@/app/(treinador)/treinador/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await waitFor(() => expect(dashboardCalls).toBe(1));
    expect(oldEndpointCalls).toBe(0);
  });
});
