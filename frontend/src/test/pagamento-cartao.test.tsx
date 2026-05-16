import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { useStripe, useElements } from "@stripe/react-stripe-js";
import { pagamentoApi } from "@/lib/api/pagamento";
import PagamentoCartao from "@/components/pagamento/PagamentoCartao";
import type { PagamentoResponse } from "@/types";
import type React from "react";

// ─── Mocks ───────────────────────────────────────────────────────────────────

vi.mock("@stripe/stripe-js", () => ({
  loadStripe: vi.fn(() => Promise.resolve(null)),
}));

vi.mock("@stripe/react-stripe-js", () => ({
  Elements: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  PaymentElement: () => <div data-testid="payment-element" />,
  useStripe: vi.fn(),
  useElements: vi.fn(),
}));

vi.mock("@/lib/api/pagamento", () => ({
  pagamentoApi: { obterPagamento: vi.fn() },
}));

// ─── Fixtures ────────────────────────────────────────────────────────────────

const BASE: PagamentoResponse = {
  pagamentoId: "p1",
  assinaturaId: "a1",
  valor: 150,
  status: "Pendente",
  metodoPagamento: "Cartao",
  pixQrCode: null,
  pixQrCodeUrl: null,
  pixExpiracao: null,
  clientSecret: "pi_test_secret_key",
  dataPagamento: null,
  createdAt: "2025-03-15T00:00:00Z",
};

function mockObter(data: Partial<PagamentoResponse>) {
  vi.mocked(pagamentoApi.obterPagamento).mockResolvedValue({
    data: { ...BASE, ...data },
  } as never);
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(useStripe).mockReturnValue(null);
  vi.mocked(useElements).mockReturnValue(null);
});

afterEach(() => vi.clearAllMocks());

// ─── Loading ─────────────────────────────────────────────────────────────────

describe("PagamentoCartao — loading", () => {
  it("exibe spinner enquanto carrega", () => {
    vi.mocked(pagamentoApi.obterPagamento).mockReturnValue(new Promise(() => {}));
    render(<PagamentoCartao pagamentoId="p1" />);
    expect(screen.getByRole("progressbar")).toBeDefined();
  });
});

// ─── Sem clientSecret ────────────────────────────────────────────────────────

describe("PagamentoCartao — sem clientSecret", () => {
  it("exibe alerta de indisponibilidade", async () => {
    mockObter({ clientSecret: null });
    render(<PagamentoCartao pagamentoId="p1" />);
    await waitFor(() => expect(screen.queryByRole("progressbar")).toBeNull());
    expect(screen.getByText(/indisponíveis/)).toBeDefined();
  });
});

// ─── Status terminal com clientSecret ────────────────────────────────────────

describe("PagamentoCartao — status terminal", () => {
  it("status Falhou → alerta de falha", async () => {
    mockObter({ status: "Falhou", clientSecret: "cs_test" });
    render(<PagamentoCartao pagamentoId="p1" />);
    await waitFor(() => expect(screen.queryByRole("progressbar")).toBeNull());
    expect(screen.getByText(/Pagamento falhou/)).toBeDefined();
  });

  it("status Expirado → alerta de expiração", async () => {
    mockObter({ status: "Expirado", clientSecret: "cs_test" });
    render(<PagamentoCartao pagamentoId="p1" />);
    await waitFor(() => expect(screen.queryByRole("progressbar")).toBeNull());
    expect(screen.getByText(/Pagamento expirado/)).toBeDefined();
  });
});

// ─── Formulário com clientSecret válido ──────────────────────────────────────

describe("PagamentoCartao — formulário", () => {
  it("clientSecret presente e status Pendente → renderiza PaymentElement", async () => {
    mockObter({ status: "Pendente", clientSecret: "cs_test" });
    render(<PagamentoCartao pagamentoId="p1" />);
    await waitFor(() => expect(screen.queryByRole("progressbar")).toBeNull());
    expect(screen.getByTestId("payment-element")).toBeDefined();
    expect(screen.getByRole("button", { name: "Pagar" })).toBeDefined();
  });

  it("status Pago dentro do form → exibe confirmação", async () => {
    mockObter({ status: "Pago", clientSecret: "cs_test" });
    render(<PagamentoCartao pagamentoId="p1" />);
    await waitFor(() => expect(screen.queryByRole("progressbar")).toBeNull());
    expect(screen.getByText("Pagamento confirmado!")).toBeDefined();
  });
});

// ─── Submit com erro Stripe ───────────────────────────────────────────────────

describe("CartaoForm — submit com erro Stripe", () => {
  it("exibe mensagem de erro e remove loading", async () => {
    const mockStripe = {
      confirmPayment: vi.fn().mockResolvedValue({ error: { message: "Card declined" } }),
    };
    vi.mocked(useStripe).mockReturnValue(mockStripe as never);
    vi.mocked(useElements).mockReturnValue({} as never);

    mockObter({ status: "Pendente", clientSecret: "cs_test" });
    render(<PagamentoCartao pagamentoId="p1" />);
    await waitFor(() => expect(screen.queryByRole("progressbar")).toBeNull());

    fireEvent.click(screen.getByRole("button", { name: "Pagar" }));

    await waitFor(() => expect(screen.getByText("Card declined")).toBeDefined());
    // botão volta a ser habilitado após erro
    expect(screen.getByRole("button", { name: "Pagar" })).not.toBeDisabled();
  });

  it("submit sem stripe/elements → não chama confirmPayment", async () => {
    const mockStripe = { confirmPayment: vi.fn() };
    vi.mocked(useStripe).mockReturnValue(null); // stripe null = guard no início do handler
    vi.mocked(useElements).mockReturnValue({} as never);

    mockObter({ status: "Pendente", clientSecret: "cs_test" });
    render(<PagamentoCartao pagamentoId="p1" />);
    await waitFor(() => expect(screen.queryByRole("progressbar")).toBeNull());

    // botão desabilitado quando stripe é null
    expect(screen.getByRole("button", { name: "Pagar" })).toBeDisabled();
    expect(mockStripe.confirmPayment).not.toHaveBeenCalled();
  });
});
