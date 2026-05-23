import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, fireEvent, act } from "@testing-library/react";

// ─── Mocks ────────────────────────────────────────────────────────────────────

vi.mock("next/link", () => ({
  default: ({ href, children }: { href: string; children: React.ReactNode }) => (
    <a href={href}>{children}</a>
  ),
}));

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), replace: vi.fn(), back: vi.fn() })),
}));

vi.mock("@/lib/api/pagamento", () => ({
  pagamentoApi: {
    obterPagamento: vi.fn(),
    verificarOnboarding: vi.fn(),
    iniciarOnboarding: vi.fn(),
    obterMinhaAssinatura: vi.fn(),
    listarPagamentosAssinatura: vi.fn(),
  },
}));

vi.mock("@/lib/api/aluno", () => ({
  alunoApi: {
    getMeuVinculo: vi.fn(),
  },
}));

import { pagamentoApi } from "@/lib/api/pagamento";
import type { PagamentoResponse, OnboardingStatusResponse } from "@/types";

// ─── Helpers ──────────────────────────────────────────────────────────────────

const makePagamento = (
  overrides: Partial<PagamentoResponse> = {}
): PagamentoResponse => ({
  pagamentoId: "pay-1",
  assinaturaId: "ass-1",
  valor: 150,
  status: "Pendente",
  metodoPagamento: "Pix",
  pixQrCode: "00020126...",
  pixQrCodeUrl: "https://img.stripe.com/qr.png",
  pixExpiracao: new Date(Date.now() + 3_600_000).toISOString(),
  clientSecret: null,
  dataPagamento: null,
  createdAt: new Date().toISOString(),
  ...overrides,
});

// ─── PagamentoPix ────────────────────────────────────────────────────────────

import PagamentoPix from "@/components/pagamento/PagamentoPix";

describe("PagamentoPix", () => {
  beforeEach(() => {
    Object.defineProperty(navigator, "clipboard", {
      value: { writeText: vi.fn().mockResolvedValue(undefined) },
      writable: true,
      configurable: true,
    });
  });

  afterEach(() => vi.clearAllMocks());

  it("exibe spinner durante carregamento inicial", () => {
    vi.mocked(pagamentoApi.obterPagamento).mockReturnValue(new Promise(() => {}));
    render(<PagamentoPix pagamentoId="pay-1" />);
    expect(screen.getByRole("progressbar")).toBeDefined();
  });

  it("estado Pago → exibe confirmação", async () => {
    vi.mocked(pagamentoApi.obterPagamento).mockResolvedValue({
      data: makePagamento({ status: "Pago" }),
    } as never);

    render(<PagamentoPix pagamentoId="pay-1" />);
    await waitFor(() =>
      expect(screen.getByText("Pagamento confirmado!")).toBeDefined()
    );
  });

  it("estado Pago → chama onPago", async () => {
    const onPago = vi.fn();
    vi.mocked(pagamentoApi.obterPagamento).mockResolvedValue({
      data: makePagamento({ status: "Pago" }),
    } as never);

    render(<PagamentoPix pagamentoId="pay-1" onPago={onPago} />);
    await waitFor(() => expect(onPago).toHaveBeenCalledOnce());
  });

  it("estado Expirado → exibe mensagem", async () => {
    vi.mocked(pagamentoApi.obterPagamento).mockResolvedValue({
      data: makePagamento({ status: "Expirado", pixQrCode: null, pixQrCodeUrl: null }),
    } as never);

    render(<PagamentoPix pagamentoId="pay-1" />);
    await waitFor(() =>
      expect(screen.getByText("Solicite uma nova cobrança ao seu treinador.")).toBeDefined()
    );
  });

  it("estado Falhou → exibe mensagem", async () => {
    vi.mocked(pagamentoApi.obterPagamento).mockResolvedValue({
      data: makePagamento({ status: "Falhou", pixQrCode: null, pixQrCodeUrl: null }),
    } as never);

    render(<PagamentoPix pagamentoId="pay-1" />);
    await waitFor(() =>
      expect(screen.getByText("Solicite uma nova cobrança ao seu treinador.")).toBeDefined()
    );
  });

  it("estado Pendente → exibe 'Pague via Pix' e botão copiar", async () => {
    vi.mocked(pagamentoApi.obterPagamento).mockResolvedValue({
      data: makePagamento(),
    } as never);

    render(<PagamentoPix pagamentoId="pay-1" />);
    await waitFor(() => expect(screen.getByText("Pague via Pix")).toBeDefined());
    expect(screen.getByText("Copiar código")).toBeDefined();
    expect(screen.getByText("Aguardando pagamento...")).toBeDefined();
  });

  it("botão copiar → escreve no clipboard", async () => {
    vi.mocked(pagamentoApi.obterPagamento).mockResolvedValue({
      data: makePagamento({ pixQrCode: "CODIGO_PIX_LONGO" }),
    } as never);

    render(<PagamentoPix pagamentoId="pay-1" />);
    await waitFor(() => expect(screen.getByText("Copiar código")).toBeDefined());
    fireEvent.click(screen.getByText("Copiar código"));
    expect(navigator.clipboard.writeText).toHaveBeenCalledWith("CODIGO_PIX_LONGO");
  });

  it("configura polling de 30s via setInterval", () => {
    const setIntervalSpy = vi.spyOn(globalThis, "setInterval");
    vi.mocked(pagamentoApi.obterPagamento).mockReturnValue(new Promise(() => {}));

    render(<PagamentoPix pagamentoId="pay-1" />);

    expect(setIntervalSpy).toHaveBeenCalledWith(expect.any(Function), 30_000);
    setIntervalSpy.mockRestore();
  });
});

// ─── PagamentosTreinadorPage ──────────────────────────────────────────────────

import PagamentosTreinadorPage from "@/app/(treinador)/treinador/pagamentos/page";

describe("PagamentosTreinadorPage", () => {
  afterEach(() => vi.clearAllMocks());

  it("exibe spinner durante carregamento", () => {
    vi.mocked(pagamentoApi.verificarOnboarding).mockReturnValue(new Promise(() => {}));
    render(<PagamentosTreinadorPage />);
    expect(screen.getByRole("progressbar")).toBeDefined();
  });

  it("onboarding completo → exibe 'Ativo'", async () => {
    const status: OnboardingStatusResponse = { onboardingCompleto: true, contaConfigurada: true };
    vi.mocked(pagamentoApi.verificarOnboarding).mockResolvedValue({ data: status } as never);

    render(<PagamentosTreinadorPage />);
    await waitFor(() => expect(screen.getByText("Ativo")).toBeDefined());
  });

  it("não configurado → exibe botão 'Configurar recebimentos'", async () => {
    const status: OnboardingStatusResponse = { onboardingCompleto: false, contaConfigurada: false };
    vi.mocked(pagamentoApi.verificarOnboarding).mockResolvedValue({ data: status } as never);

    render(<PagamentosTreinadorPage />);
    await waitFor(() =>
      expect(screen.getByText("Configurar recebimentos")).toBeDefined()
    );
  });

  it("configurado mas incompleto → exibe 'Continuar cadastro'", async () => {
    const status: OnboardingStatusResponse = { onboardingCompleto: false, contaConfigurada: true };
    vi.mocked(pagamentoApi.verificarOnboarding).mockResolvedValue({ data: status } as never);

    render(<PagamentosTreinadorPage />);
    await waitFor(() => expect(screen.getByText("Continuar cadastro")).toBeDefined());
  });

  it("clique em configurar → redireciona para URL do Stripe", async () => {
    const status: OnboardingStatusResponse = { onboardingCompleto: false, contaConfigurada: false };
    vi.mocked(pagamentoApi.verificarOnboarding).mockResolvedValue({ data: status } as never);
    vi.mocked(pagamentoApi.iniciarOnboarding).mockResolvedValue({
      data: { url: "https://connect.stripe.com/setup" },
    } as never);

    const originalLocation = window.location;
    const assignMock = vi.fn();
    Object.defineProperty(window, "location", {
      value: { ...originalLocation, href: "", origin: "https://forzion.tech" },
      writable: true,
      configurable: true,
    });

    render(<PagamentosTreinadorPage />);
    await waitFor(() => expect(screen.getByText("Configurar recebimentos")).toBeDefined());
    fireEvent.click(screen.getByText("Configurar recebimentos"));
    await waitFor(() =>
      expect(vi.mocked(pagamentoApi.iniciarOnboarding)).toHaveBeenCalledOnce()
    );

    Object.defineProperty(window, "location", { value: originalLocation, configurable: true });
  });

  it("erro na API → exibe mensagem de erro", async () => {
    vi.mocked(pagamentoApi.verificarOnboarding).mockRejectedValue(new Error("network"));

    render(<PagamentosTreinadorPage />);
    await waitFor(() =>
      expect(screen.getByText("Erro ao verificar status do cadastro Stripe.")).toBeDefined()
    );
  });
});

// ─── OnboardingRetornoPage ────────────────────────────────────────────────────

import OnboardingRetornoPage from "@/app/(treinador)/treinador/onboarding/retorno/page";

describe("OnboardingRetornoPage", () => {
  afterEach(() => vi.clearAllMocks());

  it("exibe spinner durante carregamento", () => {
    vi.mocked(pagamentoApi.verificarOnboarding).mockReturnValue(new Promise(() => {}));
    render(<OnboardingRetornoPage />);
    expect(screen.getByRole("progressbar")).toBeDefined();
  });

  it("onboarding completo → exibe 'Cadastro concluído!'", async () => {
    vi.mocked(pagamentoApi.verificarOnboarding).mockResolvedValue({
      data: { onboardingCompleto: true, contaConfigurada: true },
    } as never);

    render(<OnboardingRetornoPage />);
    await waitFor(() => expect(screen.getByText("Cadastro concluído!")).toBeDefined());
    expect(screen.getByText("Ir para recebimentos")).toBeDefined();
  });

  it("onboarding incompleto → exibe 'Cadastro incompleto'", async () => {
    vi.mocked(pagamentoApi.verificarOnboarding).mockResolvedValue({
      data: { onboardingCompleto: false, contaConfigurada: false },
    } as never);

    render(<OnboardingRetornoPage />);
    await waitFor(() => expect(screen.getByText("Cadastro incompleto")).toBeDefined());
  });

  it("erro na API → exibe 'Cadastro incompleto'", async () => {
    vi.mocked(pagamentoApi.verificarOnboarding).mockRejectedValue(new Error("fail"));

    render(<OnboardingRetornoPage />);
    await waitFor(() => expect(screen.getByText("Cadastro incompleto")).toBeDefined());
  });
});

// ─── PagamentosAlunoPage ──────────────────────────────────────────────────────

import PagamentosAlunoPage from "@/app/(aluno)/aluno/pagamentos/page";

describe("PagamentosAlunoPage", () => {
  afterEach(() => vi.clearAllMocks());

  it("estado inicial → exibe 'Nenhum pagamento encontrado.'", async () => {
    vi.mocked(pagamentoApi.obterMinhaAssinatura).mockResolvedValue({
      data: { assinaturaId: "ass-1" },
    } as never);
    vi.mocked(pagamentoApi.listarPagamentosAssinatura).mockResolvedValue({
      data: [],
    } as never);

    render(<PagamentosAlunoPage />);
    await waitFor(() =>
      expect(screen.getByText("Nenhum pagamento encontrado.")).toBeDefined()
    );
    expect(screen.getByText("Histórico de Pagamentos")).toBeDefined();
  });

  it("exibe spinner durante carregamento", () => {
    vi.mocked(pagamentoApi.obterMinhaAssinatura).mockReturnValue(new Promise(() => {}));
    render(<PagamentosAlunoPage />);
    expect(screen.getByRole("progressbar")).toBeDefined();
  });

  it("erro na API → exibe mensagem de erro", async () => {
    vi.mocked(pagamentoApi.obterMinhaAssinatura).mockRejectedValue(new Error("fail"));
    render(<PagamentosAlunoPage />);
    await waitFor(() =>
      expect(screen.getByText("Erro ao carregar pagamentos.")).toBeDefined()
    );
  });

  it("com pagamentos → exibe tabela com data, valor e status", async () => {
    vi.mocked(pagamentoApi.obterMinhaAssinatura).mockResolvedValue({
      data: { assinaturaId: "ass-1" },
    } as never);
    vi.mocked(pagamentoApi.listarPagamentosAssinatura).mockResolvedValue({
      data: [makePagamento({ valor: 99.9, status: "Pago", createdAt: "2025-03-15T00:00:00Z" })],
    } as never);

    render(<PagamentosAlunoPage />);
    await waitFor(() => expect(screen.getByText("Pago")).toBeDefined());
    expect(screen.getByText((c) => c.includes("99,90"))).toBeDefined();
  });

  it("pagamento Pendente → exibe botão 'Pagar'", async () => {
    vi.mocked(pagamentoApi.obterMinhaAssinatura).mockResolvedValue({
      data: { assinaturaId: "ass-1" },
    } as never);
    vi.mocked(pagamentoApi.listarPagamentosAssinatura).mockResolvedValue({
      data: [makePagamento({ status: "Pendente" })],
    } as never);

    render(<PagamentosAlunoPage />);
    await waitFor(() => expect(screen.getByText("Pagar")).toBeDefined());
  });

  it("pagamento Expirado → não exibe botão 'Pagar'", async () => {
    vi.mocked(pagamentoApi.obterMinhaAssinatura).mockResolvedValue({
      data: { assinaturaId: "ass-1" },
    } as never);
    vi.mocked(pagamentoApi.listarPagamentosAssinatura).mockResolvedValue({
      data: [makePagamento({ status: "Expirado", pixQrCode: null })],
    } as never);

    render(<PagamentosAlunoPage />);
    await waitFor(() => expect(screen.getByText("Expirado")).toBeDefined());
    expect(screen.queryByText("Pagar")).toBeNull();
  });

  it("pagamento Pendente Pix -> clicar Pagar abre dialog 'Pagamento via Pix'", async () => {
    vi.mocked(pagamentoApi.obterMinhaAssinatura).mockResolvedValue({
      data: { assinaturaId: "ass-1" },
    } as never);
    vi.mocked(pagamentoApi.listarPagamentosAssinatura).mockResolvedValue({
      data: [makePagamento({ status: "Pendente", metodoPagamento: "Pix" })],
    } as never);
    vi.mocked(pagamentoApi.obterPagamento).mockReturnValue(new Promise(() => {}));

    render(<PagamentosAlunoPage />);
    await waitFor(() => expect(screen.getByText("Pagar")).toBeDefined());
    fireEvent.click(screen.getByText("Pagar"));
    await waitFor(() =>
      expect(screen.getByText("Pagamento via Pix")).toBeDefined()
    );
  });
});
