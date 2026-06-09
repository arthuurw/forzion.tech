import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import Faq from "../Faq";

describe("Faq", () => {
  it("renders exactly 5 accordion items (questions)", () => {
    render(<Faq />);
    // AccordionSummary renders each question as a button role
    const buttons = screen.getAllByRole("button");
    expect(buttons).toHaveLength(5);
  });

  it("clicking an accordion item expands to show the answer", () => {
    render(<Faq />);
    const firstQuestion = screen.getByText("Meu aluno precisa pagar para usar?");
    fireEvent.click(firstQuestion);
    expect(
      screen.getByText(/quem assina o plano é o treinador/i),
    ).toBeVisible();
  });

  it("renders all 5 question texts", () => {
    render(<Faq />);
    expect(screen.getByText("Meu aluno precisa pagar para usar?")).toBeInTheDocument();
    expect(screen.getByText("Posso exportar meus dados se cancelar?")).toBeInTheDocument();
    expect(screen.getByText("Funciona no celular?")).toBeInTheDocument();
    expect(screen.getByText("Posso ter alunos de diferentes objetivos?")).toBeInTheDocument();
    expect(screen.getByText("Como funciona o cancelamento?")).toBeInTheDocument();
  });
});
