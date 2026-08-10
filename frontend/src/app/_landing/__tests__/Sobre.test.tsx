import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { axe } from "vitest-axe";
import Sobre from "../Sobre";

describe("Sobre", () => {
  it("renderiza o heading e o parágrafo de resumo", () => {
    render(<Sobre />);
    expect(
      screen.getByRole("heading", { name: /quem está por trás da forzion\.tech/i }),
    ).toBeInTheDocument();
    expect(screen.getByText(/planilha e\s*WhatsApp/i)).toBeInTheDocument();
  });

  it("CTA linka para /sobre", () => {
    render(<Sobre />);
    const link = screen.getByRole("link", { name: /conhecer a forzion\.tech/i });
    expect(link).toHaveAttribute("href", "/sobre");
  });

  it("não tem violação de axe", async () => {
    const { container } = render(<Sobre />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
