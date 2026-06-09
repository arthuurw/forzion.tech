import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";

import HowItWorks from "../HowItWorks";

describe("HowItWorks", () => {
  it("renders 3 step images with descriptive alt text", () => {
    render(<HowItWorks />);
    expect(
      screen.getByAltText("Tela de montagem de ficha de treino no painel do treinador"),
    ).toBeInTheDocument();
    expect(
      screen.getByAltText("Tela de listagem e aprovação de alunos no painel do treinador"),
    ).toBeInTheDocument();
    expect(
      screen.getByAltText("Tela de histórico de execuções no painel do aluno"),
    ).toBeInTheDocument();
  });
});
