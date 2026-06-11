import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import CheckoutTermos from "./CheckoutTermos";

describe("CheckoutTermos (R6 — CDC art. 31 transparência)", () => {
  it("exibe o valor exato formatado em BRL", () => {
    render(<CheckoutTermos valor={120} />);
    expect(screen.getByText(/R\$\s?120,00/)).toBeInTheDocument();
  });

  it("exibe a periodicidade mensal", () => {
    render(<CheckoutTermos valor={120} />);
    expect(screen.getByText(/mensal/i)).toBeInTheDocument();
  });

  it("exibe condições de cancelamento (7 dias gratuito + reembolso)", () => {
    render(<CheckoutTermos valor={120} />);
    expect(screen.getByText(/7 dias/i)).toBeInTheDocument();
    expect(screen.getByText(/reembolso/i)).toBeInTheDocument();
  });

  it("exibe a próxima data de cobrança estimada", () => {
    render(<CheckoutTermos valor={120} />);
    expect(screen.getByText(/próxima cobrança/i)).toBeInTheDocument();
  });

  it("renderiza variante densa", () => {
    render(<CheckoutTermos valor={99.9} dense />);
    expect(screen.getByText(/R\$\s?99,90/)).toBeInTheDocument();
  });
});
