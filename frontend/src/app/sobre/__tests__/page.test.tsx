import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { axe } from "vitest-axe";
import SobrePage from "../page";

describe("SobrePage", () => {
  it("renders the institutional heading", () => {
    render(<SobrePage />);
    expect(
      screen.getByRole("heading", { level: 1, name: /sobre a forzion\.tech/i }),
    ).toBeInTheDocument();
  });

  it("states who operates the platform, with razão social and CNPJ", () => {
    render(<SobrePage />);
    expect(
      screen.getByRole("heading", { name: /quem somos/i }),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/FORZIONTECH DESENVOLVIMENTO DE SOFTWARE CUSTOMIZAVEL LTDA/i),
    ).toBeInTheDocument();
    expect(screen.getByText(/67\.900\.114\/0001-69/)).toBeInTheDocument();
    expect(screen.getByText(/julho de 2026/i)).toBeInTheDocument();
  });

  it("states the platform's purpose and audience", () => {
    render(<SobrePage />);
    expect(
      screen.getByRole("heading", { name: /por que existimos/i }),
    ).toBeInTheDocument();
    expect(screen.getByText(/planilha e\s*WhatsApp/i)).toBeInTheDocument();
    expect(
      screen.getByRole("heading", { name: /a quem atendemos/i }),
    ).toBeInTheDocument();
    expect(screen.getByText(/treinadores autônomos/i)).toBeInTheDocument();
  });

  it("identifies the data controller contact channel and links the privacy policy for full detail", () => {
    render(<SobrePage />);
    expect(
      screen.getByRole("heading", { name: /encarregado de dados/i }),
    ).toBeInTheDocument();
    expect(screen.getByText(/suporte@forzion\.tech/i)).toBeInTheDocument();
    const privacyLinks = screen.getAllByRole("link", { name: /política de privacidade/i });
    expect(privacyLinks.length).toBeGreaterThan(0);
    for (const link of privacyLinks) {
      expect(link).toHaveAttribute("href", "/privacidade");
    }
  });

  it("links the accessibility page as part of the institutional set", () => {
    render(<SobrePage />);
    const link = screen.getByRole("link", { name: /acessibilidade/i });
    expect(link).toHaveAttribute("href", "/acessibilidade");
  });

  it("has no axe violations", async () => {
    const { container } = render(<SobrePage />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
