import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import type { AssinaturaTreinadorResponse, PlanoPlataformaResponse, TreinadorDashboardResponse } from "@/types";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
  useParams: () => ({}),
}));

vi.mock("@/lib/api/pagamento", () => ({
  pagamentoApi: {
    obterAssinaturaTreinador: vi.fn(),
    listarPlanosPlataforma: vi.fn(),
    cancelarPlanoTreinador: vi.fn(),
    trocarPlano: vi.fn(),
    obterStatusPagamentoTreinador: vi.fn(),
  },
}));

vi.mock("@/lib/api/treinador", () => ({
  treinadorApi: {
    getDashboard: vi.fn(),
  },
}));

vi.mock("@/lib/auth/context", () => ({
  useAuth: () => ({ logout: vi.fn() }),
}));

import { pagamentoApi } from "@/lib/api/pagamento";
import { treinadorApi } from "@/lib/api/treinador";
import PlanoTreinadorPage from "../page";

const api = vi.mocked(pagamentoApi);
const dashApi = vi.mocked(treinadorApi);

const PLANO_PRO: PlanoPlataformaResponse = {
  planoId: "plano-pro",
  nome: "Pro",
  tier: "Pro",
  descricao: null,
  maxAlunos: 30,
  preco: 100,
  isAtivo: true,
};

const ASSINATURA_BASE: AssinaturaTreinadorResponse = {
  assinaturaId: "ass-1",
  status: "Ativa",
  valor: 100,
  planoPlataformaId: "plano-pro",
  dataProximaCobranca: "2026-08-01T00:00:00Z",
  planoPlataformaIdAgendado: null,
};

function buildDashboard(overrides: Partial<TreinadorDashboardResponse["plano"]> = {}): TreinadorDashboardResponse {
  return {
    counts: { ativos: 3, aguardando: 0, inativos: 0 },
    mrr: 100,
    receitaPorPacote: [],
    totalFichas: 0,
    objetivos: [],
    pendentes: [],
    onboarding: { onboardingCompleto: true, contaConfigurada: true, modoPagamentoAluno: "Plataforma", modoPagamentoPodeAlterarEm: null },
    plano: {
      status: "Ativa",
      tierEfetivo: "Pro",
      planoContratadoId: "plano-pro",
      alunosAtivos: 3,
      capEfetivo: 30,
      excedente: 0,
      gracaAte: null,
      temCortesia: false,
      ...overrides,
    },
    dadosFiscaisPendentes: false,
  };
}

function mockOk(planoOverrides: Partial<TreinadorDashboardResponse["plano"]> = {}, assinatura = ASSINATURA_BASE) {
  api.obterAssinaturaTreinador.mockResolvedValue({ data: assinatura } as never);
  api.listarPlanosPlataforma.mockResolvedValue({ data: [PLANO_PRO] } as never);
  dashApi.getDashboard.mockResolvedValue({ data: buildDashboard(planoOverrides) } as never);
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe("PlanoTreinadorPage — badge tier efetivo (FE-01)", () => {
  it("contratado Pro + assinatura Pendente: badge mostra tier efetivo divergente do contratado", async () => {
    mockOk(
      { tierEfetivo: "Free", planoContratadoId: "plano-pro", status: "Pendente" },
      { ...ASSINATURA_BASE, status: "Pendente" },
    );
    render(<PlanoTreinadorPage />);

    expect(await screen.findByText(/Free — Pro pendente de pagamento/)).toBeInTheDocument();
  });

  it("contratado Pro + assinatura Ativa: sem badge de divergência (efetivo bate com contratado)", async () => {
    mockOk({ tierEfetivo: "Pro", planoContratadoId: "plano-pro", status: "Ativa" }, ASSINATURA_BASE);
    render(<PlanoTreinadorPage />);

    await screen.findByText("Ativa");
    expect(screen.queryByText(/pendente de pagamento/)).not.toBeInTheDocument();
  });
});

describe("PlanoTreinadorPage — gating de canais por tier efetivo (FE-05)", () => {
  it("tier efetivo Free: e-mail e whatsapp indisponíveis", async () => {
    mockOk({ tierEfetivo: "Free" });
    render(<PlanoTreinadorPage />);

    expect(await screen.findByText(/E-mail de engajamento — indisponível/)).toBeInTheDocument();
    expect(screen.getByText(/WhatsApp — indisponível/)).toBeInTheDocument();
  });

  it("tier efetivo Pro: e-mail disponível, whatsapp indisponível", async () => {
    mockOk({ tierEfetivo: "Pro" });
    render(<PlanoTreinadorPage />);

    expect(await screen.findByText(/E-mail de engajamento — disponível/)).toBeInTheDocument();
    expect(screen.getByText(/WhatsApp — indisponível/)).toBeInTheDocument();
  });

  it("tier efetivo ProPlus: e-mail e whatsapp disponíveis", async () => {
    mockOk({ tierEfetivo: "ProPlus" });
    render(<PlanoTreinadorPage />);

    expect(await screen.findByText(/E-mail de engajamento — disponível/)).toBeInTheDocument();
    expect(screen.getByText(/WhatsApp — disponível/)).toBeInTheDocument();
  });
});
