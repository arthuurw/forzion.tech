import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import Diferenciais from "../Diferenciais";

describe("Diferenciais", () => {
  it("renderiza exatamente 4 linhas de comparação", () => {
    render(<Diferenciais />);
    const aspectos = ["Foco", "Histórico", "Plano gratuito", "Acesso de alunos"];
    for (const aspecto of aspectos) {
      expect(screen.getByText(aspecto)).toBeInTheDocument();
    }
  });

  it("não contém nome de concorrente", () => {
    render(<Diferenciais />);
    expect(screen.queryByText(/trainerize/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/virtuagym/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/mindbody/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/wodify/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/glofox/i)).not.toBeInTheDocument();
  });
});
