import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type { PlanoPlataformaResponse } from "@/types";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn() })),
}));

vi.mock("@/lib/auth/context", () => ({
  useAuth: () => ({ logout: vi.fn() }),
}));

const PLANO_PRO: PlanoPlataformaResponse = {
  planoId: "plano-pro",
  nome: "Pro",
  tier: "Pro",
  descricao: null,
  maxAlunos: 30,
  preco: 100,
  isAtivo: true,
};

async function renderPage() {
  const { default: Page } = await import("../page");
  render(<Page />);
}

describe("PlanoTreinadorPage — contratar Pix sem QR", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("mostra erro quando contratacao Pix retorna sem pixQrCode em vez de tela de pagamento muda", async () => {
    server.use(
      http.get("*/treinador/plano/assinatura", () => HttpResponse.json(null)),
      http.get("*/auth/planos", () => HttpResponse.json([PLANO_PRO])),
      http.get("*/treinador/dashboard", () =>
        HttpResponse.json({
          counts: { ativos: 0, aguardando: 0, inativos: 0 },
          mrr: 0,
          receitaPorPacote: [],
          totalFichas: 0,
          objetivos: [],
          pendentes: [],
          onboarding: { onboardingCompleto: true, contaConfigurada: true, modoPagamentoAluno: "Plataforma", modoPagamentoPodeAlterarEm: null },
          plano: {
            status: null,
            tierEfetivo: "Free",
            planoContratadoId: null,
            alunosAtivos: 0,
            capEfetivo: 0,
            excedente: 0,
            gracaAte: null,
            temCortesia: false,
          },
          dadosFiscaisPendentes: false,
        }),
      ),
      http.post("*/treinador/plano/contratar", () =>
        HttpResponse.json({
          pagamentoId: "pg-1",
          valorPagamento: 100,
          metodoPagamento: "Pix",
          pixQrCode: null,
          pixQrCodeUrl: null,
          pixExpiracao: null,
          clientSecret: null,
        }),
      ),
    );
    await renderPage();

    const contratar = await screen.findAllByRole("button", { name: "Contratar" });
    fireEvent.click(contratar[contratar.length - 1]);

    fireEvent.click(await screen.findByRole("button", { name: "Confirmar" }));

    expect(await screen.findByText(/Não foi possível gerar o QR code Pix/)).toBeInTheDocument();
  });
});
