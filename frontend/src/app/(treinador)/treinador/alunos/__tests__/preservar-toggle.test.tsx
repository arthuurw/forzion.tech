import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type { VinculoDetalheResponse, TreinadorDashboardResponse } from "@/types";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn() })),
  useParams: vi.fn(() => ({})),
}));

function buildVinculo(overrides: Partial<VinculoDetalheResponse> = {}): VinculoDetalheResponse {
  return {
    vinculoId: "v-1",
    treinadorId: "t-1",
    alunoId: "a-1",
    nomeAluno: "João Aluno",
    emailAluno: "joao@test.com",
    status: "Ativo",
    createdAt: "2025-01-01T00:00:00Z",
    pacoteId: "pac-1",
    temVinculoAtivoPrevio: false,
    preservarNoLimite: false,
    ...overrides,
  };
}

function buildDashboard(gracaAte: string | null, excedente: number): TreinadorDashboardResponse {
  return {
    counts: { ativos: 1, aguardando: 0, inativos: 0 },
    mrr: 0,
    receitaPorPacote: [],
    totalFichas: 0,
    objetivos: [],
    pendentes: [],
    onboarding: { onboardingCompleto: true, contaConfigurada: true, modoPagamentoAluno: "Plataforma", modoPagamentoPodeAlterarEm: null },
    plano: {
      status: "Ativa",
      tierEfetivo: "Free",
      planoContratadoId: null,
      alunosAtivos: 1,
      capEfetivo: 5,
      excedente,
      gracaAte,
      temCortesia: false,
    },
    dadosFiscaisPendentes: false,
  };
}

function setupVinculos(vinculos: VinculoDetalheResponse[]) {
  server.use(
    http.get("*/treinador/vinculos", () =>
      HttpResponse.json({ items: vinculos, total: vinculos.length, pagina: 1, tamanhoPagina: 10 })),
  );
}

async function renderPage() {
  const { default: Page } = await import("../page");
  render(<Page />);
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe("AlunosTreinadorPage — banner de graça (FE-03/NOTIF-05)", () => {
  it("gracaAte definido: banner exibe excedente e data limite", async () => {
    setupVinculos([buildVinculo()]);
    server.use(
      http.get("*/treinador/dashboard", () => HttpResponse.json(buildDashboard("2026-09-15T12:00:00Z", 3))),
    );
    await renderPage();

    expect(await screen.findByText(/Faltam inativar 3 aluno\(s\) até 15\/09\/2026/)).toBeInTheDocument();
  });

  it("sem graça ativa (gracaAte nulo): banner não aparece", async () => {
    setupVinculos([buildVinculo()]);
    server.use(
      http.get("*/treinador/dashboard", () => HttpResponse.json(buildDashboard(null, 0))),
    );
    await renderPage();

    await screen.findByText("João Aluno");
    expect(screen.queryByText(/Faltam inativar/)).not.toBeInTheDocument();
  });
});

describe("AlunosTreinadorPage — toggle preservar no limite (FE-04)", () => {
  it("reflete o estado persistido (preservarNoLimite=true) já marcado no load", async () => {
    setupVinculos([buildVinculo({ preservarNoLimite: true })]);
    server.use(
      http.get("*/treinador/dashboard", () => HttpResponse.json(buildDashboard(null, 0))),
    );
    await renderPage();

    const checkbox = await screen.findByRole("checkbox", { name: /manter/i });
    expect(checkbox).toBeChecked();
  });

  it("clicar no toggle chama PATCH com vinculoId e boolean corretos e atualiza o estado marcado", async () => {
    setupVinculos([buildVinculo({ preservarNoLimite: false })]);
    server.use(
      http.get("*/treinador/dashboard", () => HttpResponse.json(buildDashboard(null, 0))),
    );

    let capturedBody: unknown = null;
    let capturedId: string | null = null;
    server.use(
      http.patch("*/treinador/alunos/:id/preservar", async ({ request, params }) => {
        capturedId = params.id as string;
        capturedBody = await request.json();
        return HttpResponse.json({ vinculoId: params.id, preservarNoLimite: true });
      }),
    );

    await renderPage();
    const checkbox = await screen.findByRole("checkbox", { name: /manter/i });
    expect(checkbox).not.toBeChecked();

    fireEvent.click(checkbox);

    await waitFor(() => expect(checkbox).toBeChecked());
    expect(capturedId).toBe("v-1");
    expect(capturedBody).toEqual({ preservar: true });
  });
});
