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
import { buildPagamento } from "@/test/factories";
import PagamentoCartao from "@/components/pagamento/PagamentoCartao";
import type { PagamentoResponse } from "@/types";
import type React from "react";

// Stripe mock: F7 territory, not F6.
vi.mock("@stripe/stripe-js", () => ({
  loadStripe: vi.fn(() => Promise.resolve(null)),
}));

vi.mock("@stripe/react-stripe-js", () => ({
  Elements: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  PaymentElement: () => <div data-testid="payment-element" />,
  useStripe: vi.fn(),
  useElements: vi.fn(),
}));

// F36: fixtures consolidados via buildPagamento. Defaults pra este spec: Cartao Pendente com clientSecret valido.
const CARTAO_DEFAULTS: Partial<PagamentoResponse> = {
  pagamentoId: "p1",
  assinaturaAlunoId: "a1",
  valor: 150,
  status: "Pendente",
  metodoPagamento: "Cartao",
  clientSecret: "pi_test_secret_key",
  dataPagamento: null,
  createdAt: "2025-03-15T00:00:00Z",
};

function respondPagamento(overrides: Partial<PagamentoResponse>) {
  server.use(
    http.get("*/aluno/pagamentos/:id", () =>
      HttpResponse.json(buildPagamento({ ...CARTAO_DEFAULTS, ...overrides })),
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

describe("PagamentoCartao — loading", () => {
  it("exibe spinner enquanto carrega", () => {
    hangPagamento();
    render(<PagamentoCartao pagamentoId="p1" />);
    expect(screen.getByRole("progressbar")).toBeInTheDocument();
  });
});

describe("PagamentoCartao — sem clientSecret", () => {
  it("exibe alerta de indisponibilidade", async () => {
    respondPagamento({ clientSecret: null });
    render(<PagamentoCartao pagamentoId="p1" />);
    await waitFor(() => expect(screen.queryByRole("progressbar")).not.toBeInTheDocument());
    expect(screen.getByText(/indisponíveis/)).toBeInTheDocument();
  });
});

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

// F7: Stripe partial mock — args + success + processando.
// Mantemos Elements/PaymentElement mockados (DOM), mas useStripe/useElements
// retornam OBJETOS REALISTAS (com confirmPayment configuravel). Cobre:
// - args passados pro confirmPayment (elements ref, return_url, redirect)
// - happy path (resolve sem error) → onPago chamado
// - estado "processando" durante await (CircularProgress visivel)
// - erro generico (sem error.message) → fallback string
//
// Stripe.js completo (Elements DOM/clientSecret network) fica out-of-scope —
// testado em E2E Playwright (F3/F29).

describe("CartaoForm — F7 partial Stripe mock", () => {
  function realisticStripe(confirmPayment: ReturnType<typeof vi.fn>) {
    // Objeto realista — mesma shape dos retornos da Stripe.js. Outros metodos
    // (createToken, retrievePaymentIntent, etc) ficam fora pq o componente nao
    // usa — adicionar conforme precisar.
    return { confirmPayment } as never;
  }

  function realisticElements() {
    // Container Elements opaco — o componente passa `elements` direto pro
    // confirmPayment; nao introspecciona. Vazio basta.
    return {} as never;
  }

  it("confirmPayment recebe elements + return_url=window.location.href + redirect='if_required'", async () => {
    const confirmPayment = vi.fn().mockResolvedValue({});
    const elements = realisticElements();
    vi.mocked(useStripe).mockReturnValue(realisticStripe(confirmPayment));
    vi.mocked(useElements).mockReturnValue(elements);

    respondPagamento({ status: "Pendente", clientSecret: "cs_test" });
    render(<PagamentoCartao pagamentoId="p1" />);
    await waitFor(() => expect(screen.queryByRole("progressbar")).not.toBeInTheDocument());

    fireEvent.click(screen.getByRole("button", { name: "Pagar" }));

    await waitFor(() => expect(confirmPayment).toHaveBeenCalledTimes(1));
    expect(confirmPayment).toHaveBeenCalledWith({
      elements,
      confirmParams: { return_url: window.location.href },
      redirect: "if_required",
    });
  });

  it("success path (Stripe resolve sem error) → onPago é chamado", async () => {
    const confirmPayment = vi.fn().mockResolvedValue({}); // sem error
    vi.mocked(useStripe).mockReturnValue(realisticStripe(confirmPayment));
    vi.mocked(useElements).mockReturnValue(realisticElements());

    const onPago = vi.fn();
    respondPagamento({ status: "Pendente", clientSecret: "cs_test" });
    render(<PagamentoCartao pagamentoId="p1" onPago={onPago} />);
    await waitFor(() => expect(screen.queryByRole("progressbar")).not.toBeInTheDocument());

    fireEvent.click(screen.getByRole("button", { name: "Pagar" }));

    await waitFor(() => expect(onPago).toHaveBeenCalledTimes(1));
  });

  it("durante submit (confirmPayment pendente) → mostra CircularProgress + botao disabled", async () => {
    // Promessa controlada — segura o estado "processando" pra observar.
    let resolveConfirm: (v: object) => void = () => {};
    const confirmPayment = vi.fn(
      () => new Promise<object>((r) => { resolveConfirm = r; }),
    );
    vi.mocked(useStripe).mockReturnValue(realisticStripe(confirmPayment));
    vi.mocked(useElements).mockReturnValue(realisticElements());

    respondPagamento({ status: "Pendente", clientSecret: "cs_test" });
    render(<PagamentoCartao pagamentoId="p1" />);
    await waitFor(() => expect(screen.queryByRole("progressbar")).not.toBeInTheDocument());

    fireEvent.click(screen.getByRole("button", { name: "Pagar" }));

    // Loading do submit (CircularProgress aparece DENTRO do botao "Pagar").
    await waitFor(() => {
      expect(screen.getByRole("progressbar")).toBeInTheDocument();
    });
    // Botao disabled durante processando — query por type submit pra
    // diferenciar do botao "Pagar" inicial.
    const submitBtn = screen.getByRole("button");
    expect(submitBtn).toBeDisabled();

    resolveConfirm({});
  });

  it("erro sem message → fallback 'Erro ao processar pagamento.'", async () => {
    const confirmPayment = vi.fn().mockResolvedValue({ error: {} });
    vi.mocked(useStripe).mockReturnValue(realisticStripe(confirmPayment));
    vi.mocked(useElements).mockReturnValue(realisticElements());

    respondPagamento({ status: "Pendente", clientSecret: "cs_test" });
    render(<PagamentoCartao pagamentoId="p1" />);
    await waitFor(() => expect(screen.queryByRole("progressbar")).not.toBeInTheDocument());

    fireEvent.click(screen.getByRole("button", { name: "Pagar" }));

    expect(await screen.findByText("Erro ao processar pagamento.")).toBeInTheDocument();
  });
});
