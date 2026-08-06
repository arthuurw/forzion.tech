import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import PrivacidadePage from "../page";

describe("PrivacidadePage", () => {
  it("renders the privacy policy heading", () => {
    render(<PrivacidadePage />);
    expect(
      screen.getByRole("heading", { level: 1, name: /política de privacidade/i }),
    ).toBeInTheDocument();
  });

  it("lists the three sub-processors (Resend, Stripe, Meta)", () => {
    render(<PrivacidadePage />);
    expect(screen.getByText("Resend")).toBeInTheDocument();
    expect(screen.getByText("Stripe")).toBeInTheDocument();
    expect(screen.getByText(/Meta \(WhatsApp Cloud API\)/i)).toBeInTheDocument();
  });

  it("states the processing purpose and PII categories, including health data", () => {
    render(<PrivacidadePage />);
    expect(
      screen.getByRole("heading", { name: /finalidade do tratamento/i }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("heading", { name: /categorias de dados pessoais/i }),
    ).toBeInTheDocument();
    expect(screen.getByText(/anamnese/i)).toBeInTheDocument();
  });

  it("discloses treinador fiscal data as a PII category", () => {
    render(<PrivacidadePage />);
    expect(
      screen.getByText(/CPF\/CNPJ, razão social, inscrição municipal/i),
    ).toBeInTheDocument();
  });

  it("discloses legal retention of fiscal/transactional data after account deletion", () => {
    render(<PrivacidadePage />);
    expect(
      screen.getByRole("heading", { name: /retenção e exclusão de dados/i }),
    ).toBeInTheDocument();
    expect(screen.getByText(/art\. 7º, II e art\. 16, I da LGPD/i)).toBeInTheDocument();
  });

  it("mentions international data transfer", () => {
    render(<PrivacidadePage />);
    expect(
      screen.getByRole("heading", { name: /transferência internacional/i }),
    ).toBeInTheDocument();
    expect(screen.getByText(/fora do Brasil/i)).toBeInTheDocument();
  });

  it("clearly marks the content as a preliminary placeholder, not final legal text", () => {
    render(<PrivacidadePage />);
    const aviso = screen.getByRole("note");
    expect(aviso).toHaveTextContent(/preliminar/i);
    expect(aviso).toHaveTextContent(/não constitui a versão final/i);
  });
});
