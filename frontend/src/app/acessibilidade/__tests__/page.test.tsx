import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import AcessibilidadePage from "../page";

describe("AcessibilidadePage", () => {
  it("renders the accessibility heading", () => {
    render(<AcessibilidadePage />);
    expect(
      screen.getByRole("heading", { level: 1, name: /acessibilidade/i }),
    ).toBeInTheDocument();
  });

  it("declares the WCAG 2.1 AA standard, evaluation date and audited scope", () => {
    render(<AcessibilidadePage />);
    expect(
      screen.getByRole("heading", { name: /padrão perseguido, data e escopo/i }),
    ).toBeInTheDocument();
    expect(screen.getByText(/WCAG 2\.1, nível AA/i)).toBeInTheDocument();
    expect(screen.getByText(/2026-08-09/)).toBeInTheDocument();
  });

  it("states the conformance status derived from the audit document, without a new unverified claim", () => {
    render(<AcessibilidadePage />);
    expect(
      screen.getByRole("heading", { name: /estado de conformidade/i }),
    ).toBeInTheDocument();
    expect(screen.getByText(/NVDA \(Windows\) e/i)).toBeInTheDocument();
    expect(screen.getByText(/não encontrou nenhum achado/i)).toBeInTheDocument();
    expect(
      screen.getByText(/aguardam confirmação de\s*execução no ambiente de integração contínua/i),
    ).toBeInTheDocument();
  });

  it("discloses known non-conformances with no omission", () => {
    render(<AcessibilidadePage />);
    expect(
      screen.getByRole("heading", { name: /não-conformidades conhecidas/i }),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/Nenhuma não-conformidade foi encontrada até o momento/i),
    ).toBeInTheDocument();
  });

  it("provides a barrier-report contact channel with a response deadline", () => {
    render(<AcessibilidadePage />);
    expect(
      screen.getByRole("heading", { name: /relatar uma barreira/i }),
    ).toBeInTheDocument();
    expect(screen.getByText(/suporte@forzion\.tech/i)).toBeInTheDocument();
    expect(screen.getByText(/15 dias/i)).toBeInTheDocument();
  });
});
