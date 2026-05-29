// F6b (Fase 3 test remediation) — migrado de vi.mock("@/lib/api/pagamento")
// pra MSW. apiClient real envia GET /aluno/pagamentos/:id; MSW intercepta.
// Pega bugs de URL templating/serializacao escondidos pelo mock antigo.
//
// @stripe/* continua mockado: F7 (componente Stripe partial mock) ainda
// pendente — fora do scope F6 (separacao de concerns).
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { useStripe, useElements } from "@stripe/react-stripe-js";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import PagamentoCartao from "@/components/pagamento/PagamentoCartao";
import type { PagamentoResponse } from "@/types";
import type React from "react";

// ─── Stripe mock (F7 territory, not F6) ──────────────────────────────────────

vi.mock("@stripe/stripe-js", () => ({
  loadStripe: vi.fn(() => Promise.resolve(null)),
}));

vi.mock("@stripe/react-stripe-js", () => ({
  Elements: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  PaymentElement: () => <div data-testid="payment-element" />,
  useStripe: vi.fn(),
  useElements: vi.fn(),
}));

// ─── Fixtures ────────────────────────────────────────────────────────────────

const BASE: PagamentoResponse = {
  pagamentoId: "p1",
  assinaturaAlunoId: "a1",
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

function respondPagamento(overrides: Partial<PagamentoResponse>) {
  server.use(
    http.get("*/aluno/pagamentos/:id", () =>
      HttpResponse.json({ ...BASE, ...overrides }),
    ),
  );
}

function hangPagamento() {
  server.use(
    http.get("*/aluno/pagamentos/:id", () => new Promise<Response>(() => {})),
  );
}

beforeEach(() => {
  vi.mocked(useStripe).mockReturnValue(null);
  vi.mocked(useElements).mockReturnValue(null);
});

afterEach(() => vi.clearAllMocks());

// ─── Loading ─────────────────────────────────────────────────────────────────

describe("PagamentoCartao — loading", () => {
  it("exibe spinner enquanto carrega", () => {
    hangPagamento();
    render(<PagamentoCartao pagamentoId="p1" />);
    expect(screen.getByRole("progressbar")).toBeInTheDocument();
  });
});

// ─── Sem clientSecret ────────────────────────────────────────────────────────

describe("PagamentoCartao — sem clientSecret", () => {
  it("exibe alerta de indisponibilidade", async () => {
    respondPagamento({ clientSecret: null });
    render(<PagamentoCartao pagamentoId="p1" />);
    await waitFor(() => expect(screen.queryByRole("progressbar")).not.toBeInTheDocument());
    expect(screen.getByText(/indisponíveis/)).toBeInTheDocument();
  });
});

// ─── Status terminal com clientSecret ────────────────────────────────────────

describe("PagamentoCartao — status terminal", () => {
  it("status Falhou → alerta de falha", async () => {
    respondPagamento({ status: "Falhou", clientSecret: "cs_test" });
    render(<PagamentoCartao pagamentoId="p1" />);
    await waitFor(() => expect(screen.queryByRole("progressbar")).not.toBeInTheDocument());
    expect(screen.getByText(/Pagamento falhou/)).toBeInTheDocument();
  });

  it("status Expirado → alerta de expiração", async () => {
    respondPagamento({ status: "Expirado", clientSecret: "cs_test" });
    render(<PagamentoCartao pagamentoId="p1" />);
    await waitFor(() => expect(screen.queryByRole("progressbar")).not.toBeInTheDocument());
    expect(screen.getByText(/Pagamento expirado/)).toBeInTheDocument();
  });
});

// ─── Formulário com clientSecret válido ──────────────────────────────────────

describe("PagamentoCartao — formulário", () => {
  it("clientSecret presente e status Pendente → renderiza PaymentElement", async () => {
    respondPagamento({ status: "Pendente", clientSecret: "cs_test" });
    render(<PagamentoCartao pagamentoId="p1" />);
    await waitFor(() => expect(screen.queryByRole("progressbar")).not.toBeInTheDocument());
    expect(screen.getByTestId("payment-element")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Pagar" })).toBeInTheDocument();
  });

  it("status Pago dentro do form → exibe confirmação", async () => {
    respondPagamento({ status: "Pago", clientSecret: "cs_test" });
    render(<PagamentoCartao pagamentoId="p1" />);
    await waitFor(() => expect(screen.queryByRole("progressbar")).not.toBeInTheDocument());
    expect(screen.getByText("Pagamento confirmado!")).toBeInTheDocument();
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

    respondPagamento({ status: "Pendente", clientSecret: "cs_test" });
    render(<PagamentoCartao pagamentoId="p1" />);
    await waitFor(() => expect(screen.queryByRole("progressbar")).not.toBeInTheDocument());

    fireEvent.click(screen.getByRole("button", { name: "Pagar" }));

    expect(await screen.findByText("Card declined")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Pagar" })).toBeEnabled();
  });

  it("submit sem stripe/elements → não chama confirmPayment", async () => {
    const mockStripe = { confirmPayment: vi.fn() };
    vi.mocked(useStripe).mockReturnValue(null);
    vi.mocked(useElements).mockReturnValue({} as never);

    respondPagamento({ status: "Pendente", clientSecret: "cs_test" });
    render(<PagamentoCartao pagamentoId="p1" />);
    await waitFor(() => expect(screen.queryByRole("progressbar")).not.toBeInTheDocument());

    expect(screen.getByRole("button", { name: "Pagar" })).toBeDisabled();
    expect(mockStripe.confirmPayment).not.toHaveBeenCalled();
  });
});
