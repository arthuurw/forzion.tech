import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import type { AssinaturaTreinadorResponse, PlanoPlataformaResponse, TreinadorDashboardResponse } from "@/types";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
  useParams: () => ({}),
}));

vi.mock("@/lib/api/pagamento", () => ({
  pagamentoApi: {
    obterAssinaturaTreinador: vi.fn(),
    listarPlanosPlataforma: vi.fn(),
    trocarPlano: vi.fn(),
    obterStatusPagamentoTreinador: vi.fn(),
    cancelarPlanoTreinador: vi.fn(),
  },
}));

vi.mock("@/lib/api/treinador", () => ({
  treinadorApi: { getDashboard: vi.fn() },
}));

vi.mock("@/lib/auth/context", () => ({
  useAuth: () => ({ logout: vi.fn() }),
}));

import { pagamentoApi } from "@/lib/api/pagamento";
import { treinadorApi } from "@/lib/api/treinador";
import PlanoTreinadorPage from "../page";

const api = vi.mocked(pagamentoApi);
const dashApi = vi.mocked(treinadorApi);

const PLANO_PROPLUS: PlanoPlataformaResponse = {
  planoId: "plano-proplus",
  nome: "Pro+",
  tier: "ProPlus",
  descricao: null,
  maxAlunos: 999,
  preco: 300,
  isAtivo: true,
};

const PLANO_BASIC: PlanoPlataformaResponse = {
  planoId: "plano-basic",
  nome: "Basic",
  tier: "Basic",
  descricao: null,
  maxAlunos: 5,
  preco: 50,
  isAtivo: true,
};

const PLANO_PRO: PlanoPlataformaResponse = {
  planoId: "plano-pro",
  nome: "Pro",
  tier: "Pro",
  descricao: null,
  maxAlunos: 100,
  preco: 150,
  isAtivo: true,
};

const ASSINATURA_PROPLUS: AssinaturaTreinadorResponse = {
  assinaturaId: "ass-1",
  status: "Ativa",
  valor: 300,
  planoPlataformaId: "plano-proplus",
  dataProximaCobranca: new Date(Date.now() + 15 * 24 * 60 * 60 * 1000).toISOString(),
  planoPlataformaIdAgendado: null,
};

function buildDashboard(alunosAtivos: number): TreinadorDashboardResponse {
  return {
    counts: { ativos: alunosAtivos, aguardando: 0, inativos: 0 },
    mrr: 300,
    receitaPorPacote: [],
    totalFichas: 0,
    objetivos: [],
    pendentes: [],
    onboarding: { onboardingCompleto: true, contaConfigurada: true, modoPagamentoAluno: "Plataforma", modoPagamentoPodeAlterarEm: null },
    plano: {
      status: "Ativa",
      tierEfetivo: "ProPlus",
      planoContratadoId: "plano-proplus",
      alunosAtivos,
      capEfetivo: 999,
      excedente: 0,
      gracaAte: null,
      temCortesia: false,
    },
    dadosFiscaisPendentes: false,
  };
}

function mockOk(alunosAtivos: number, planos: PlanoPlataformaResponse[]) {
  api.obterAssinaturaTreinador.mockResolvedValue({ data: ASSINATURA_PROPLUS } as never);
  api.listarPlanosPlataforma.mockResolvedValue({ data: planos } as never);
  dashApi.getDashboard.mockResolvedValue({ data: buildDashboard(alunosAtivos) } as never);
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe("PlanoTreinadorPage — impacto de downgrade X/Y/Z (FE-02)", () => {
  it("ProPlus com 80 ativos trocando para Basic (cap 5): mostra comporta 5, tem 80, inativar 75", async () => {
    mockOk(80, [PLANO_BASIC, PLANO_PROPLUS]);
    render(<PlanoTreinadorPage />);

    const trocar = await screen.findAllByRole("button", { name: "Trocar" });
    fireEvent.click(trocar[0]);

    expect(await screen.findByText("Confirmar troca de plano")).toBeInTheDocument();
    expect(
      screen.getByText("Este plano comporta 5. Você tem 80. Precisará inativar 75."),
    ).toBeInTheDocument();
  });

  it("upgrade (cap do novo plano suficiente): não mostra aviso de impacto", async () => {
    mockOk(80, [PLANO_PRO, PLANO_PROPLUS]);
    render(<PlanoTreinadorPage />);

    const trocar = await screen.findAllByRole("button", { name: "Trocar" });
    fireEvent.click(trocar[0]);

    await screen.findByText("Confirmar troca de plano");
    expect(screen.queryByText(/Precisará inativar/)).not.toBeInTheDocument();
  });

  it("mesmo cap do plano atual: não mostra aviso de impacto", async () => {
    mockOk(3, [PLANO_PRO, PLANO_PROPLUS]);
    render(<PlanoTreinadorPage />);

    const trocar = await screen.findAllByRole("button", { name: "Trocar" });
    fireEvent.click(trocar[0]);

    await screen.findByText("Confirmar troca de plano");
    expect(screen.queryByText(/Precisará inativar/)).not.toBeInTheDocument();
  });
});
