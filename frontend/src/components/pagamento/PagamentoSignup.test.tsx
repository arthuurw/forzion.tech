import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { useStripe, useElements } from "@stripe/react-stripe-js";
import type React from "react";
import PagamentoSignup from "./PagamentoSignup";
import type { IniciarPagamentoPlanoResponse } from "@/types";

vi.mock("@stripe/stripe-js", () => ({
  loadStripe: vi.fn(() => Promise.resolve(null)),
}));

vi.mock("@stripe/react-stripe-js", () => ({
  Elements: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  PaymentElement: () => <div data-testid="payment-element" />,
  useStripe: vi.fn(),
  useElements: vi.fn(),
}));

const BASE: IniciarPagamentoPlanoResponse = {
  pagamentoId: "pg-1",
  valor: 50,
  status: "Pendente",
  metodoPagamento: "Pix",
  stripePaymentIntentId: null,
  pixQrCode: null,
  pixQrCodeUrl: null,
  pixExpiracao: null,
  clientSecret: null,
  createdAt: "2026-06-05T00:00:00Z",
};

beforeEach(() => {
  vi.mocked(useStripe).mockReturnValue(null);
  vi.mocked(useElements).mockReturnValue(null);
});
afterEach(() => vi.clearAllMocks());

describe("PagamentoSignup — Pix", () => {
  it("exibe valor, código copia-cola e aviso de verificação", () => {
    render(<PagamentoSignup pagamento={{ ...BASE, pixQrCode: "00020126abc" }} onPagoCartao={vi.fn()} />);
    expect(screen.getByText("Pague via Pix")).toBeInTheDocument();
    expect(screen.getByText(/00020126abc/)).toBeInTheDocument();
    expect(screen.getByText(/confirmarmos o pagamento/i)).toBeInTheDocument();
  });
});

describe("PagamentoSignup — Cartão", () => {
  it("sem clientSecret exibe alerta de indisponibilidade", () => {
    render(<PagamentoSignup pagamento={{ ...BASE, metodoPagamento: "Cartao", clientSecret: null }} onPagoCartao={vi.fn()} />);
    expect(screen.getByText(/indisponíveis/)).toBeInTheDocument();
  });

  it("com clientSecret renderiza PaymentElement e botão Pagar", () => {
    render(<PagamentoSignup pagamento={{ ...BASE, metodoPagamento: "Cartao", clientSecret: "cs_test" }} onPagoCartao={vi.fn()} />);
    expect(screen.getByTestId("payment-element")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Pagar" })).toBeInTheDocument();
  });

  it("confirmPayment sem erro chama onPagoCartao", async () => {
    const confirmPayment = vi.fn().mockResolvedValue({});
    vi.mocked(useStripe).mockReturnValue({ confirmPayment } as never);
    vi.mocked(useElements).mockReturnValue({} as never);
    const onPagoCartao = vi.fn();

    render(<PagamentoSignup pagamento={{ ...BASE, metodoPagamento: "Cartao", clientSecret: "cs_test" }} onPagoCartao={onPagoCartao} />);
    fireEvent.click(screen.getByRole("button", { name: "Pagar" }));

    await waitFor(() => expect(onPagoCartao).toHaveBeenCalledTimes(1));
  });

  it("confirmPayment com erro exibe mensagem pt-BR e não chama onPagoCartao", async () => {
    const confirmPayment = vi.fn().mockResolvedValue({ error: { decline_code: "insufficient_funds" } });
    vi.mocked(useStripe).mockReturnValue({ confirmPayment } as never);
    vi.mocked(useElements).mockReturnValue({} as never);
    const onPagoCartao = vi.fn();

    render(<PagamentoSignup pagamento={{ ...BASE, metodoPagamento: "Cartao", clientSecret: "cs_test" }} onPagoCartao={onPagoCartao} />);
    fireEvent.click(screen.getByRole("button", { name: "Pagar" }));

    expect(await screen.findByText("Saldo ou limite insuficiente. Tente outro cartão.")).toBeInTheDocument();
    expect(onPagoCartao).not.toHaveBeenCalled();
  });
});
