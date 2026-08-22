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

  it("lists the five real sub-processors and omits Meta", () => {
    render(<PrivacidadePage />);
    expect(screen.getByText("Resend")).toBeInTheDocument();
    expect(screen.getByText("Stripe")).toBeInTheDocument();
    expect(screen.getByText("Supabase")).toBeInTheDocument();
    expect(screen.getByText("Sentry")).toBeInTheDocument();
    expect(screen.getByText("Cloudflare R2")).toBeInTheDocument();
    expect(screen.queryByText(/Meta \(WhatsApp/i)).not.toBeInTheDocument();
  });

  it("states the processing purpose and PII categories with base legal, including health data under art. 11", () => {
    render(<PrivacidadePage />);
    expect(
      screen.getByRole("heading", { name: /finalidade do tratamento/i }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("heading", { name: /categorias de dados pessoais e base legal/i }),
    ).toBeInTheDocument();
    expect(screen.getByText(/anamnese/i)).toBeInTheDocument();
    expect(screen.getByText(/consentimento específico e destacado \(art\. 11/i)).toBeInTheDocument();
  });

  it("discloses treinador fiscal data as a PII category", () => {
    render(<PrivacidadePage />);
    expect(
      screen.getByText(/CPF\/CNPJ, razão social, inscrição municipal/i),
    ).toBeInTheDocument();
  });

  it("discloses legal retention of fiscal/transactional data after account deletion, plus token/notification retentions", () => {
    render(<PrivacidadePage />);
    expect(
      screen.getByRole("heading", { name: /retenção e exclusão de dados/i }),
    ).toBeInTheDocument();
    expect(screen.getByText(/art\. 7º, II e art\. 16, I da LGPD/i)).toBeInTheDocument();
    expect(screen.getByText(/90 dias/i)).toBeInTheDocument();
  });

  it("declares international transfer with the art. 33 VII legal basis", () => {
    render(<PrivacidadePage />);
    expect(
      screen.getByRole("heading", { name: /transferência internacional/i }),
    ).toBeInTheDocument();
    expect(screen.getByText(/art\. 33, VII da LGPD/i)).toBeInTheDocument();
  });

  it("declares the treatment of lead data: purpose, legal basis, 180-day retention and support channel", () => {
    render(<PrivacidadePage />);
    expect(
      screen.getByRole("heading", { name: /dados de leads \(pessoas sem conta\)/i }),
    ).toBeInTheDocument();
    expect(screen.getByText(/consentimento \(art\. 7º, i da lgpd\)/i)).toBeInTheDocument();
    expect(screen.getByText(/180 dias sem qualquer/i)).toBeInTheDocument();
    expect(screen.getAllByText(/suporte@forzion\.tech/i).length).toBeGreaterThan(0);
  });

  it("identifies the DPO contact channel and the request-handling procedure", () => {
    render(<PrivacidadePage />);
    expect(
      screen.getByRole("heading", { name: /encarregado de dados \(dpo\)/i }),
    ).toBeInTheDocument();
    expect(screen.getAllByText(/suporte@forzion\.tech/i).length).toBeGreaterThan(0);
    expect(screen.getByText(/15 dias \(art\. 19, LGPD\)/i)).toBeInTheDocument();
  });

  it("clearly marks the content as a dated preliminary version, not final legal text", () => {
    render(<PrivacidadePage />);
    const aviso = screen.getByRole("note");
    expect(aviso).toHaveTextContent(/versão preliminar, publicada em 2026-08-09/i);
    expect(aviso).toHaveTextContent(/revisão jurídica formal/i);
  });
});
