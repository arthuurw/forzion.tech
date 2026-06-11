import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";

import HowItWorks from "../HowItWorks";

describe("HowItWorks", () => {
  it("renders 3 step mockups with descriptive labels", () => {
    render(<HowItWorks />);
    expect(
      screen.getByRole("img", { name: "Ficha de treino: exercícios, séries e observações" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("img", { name: "Carteira de alunos com status e ações" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("img", { name: "Histórico de execuções: frequência e progressão" }),
    ).toBeInTheDocument();
  });
});
