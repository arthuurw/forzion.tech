import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import GracaLimiteBanner from "../GracaLimiteBanner";

describe("GracaLimiteBanner", () => {
  it("gracaAte nulo: não renderiza nada", () => {
    render(<GracaLimiteBanner gracaAte={null} excedente={0} />);
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("gracaAte definido mas excedente zerado (stale): não renderiza nada", () => {
    render(<GracaLimiteBanner gracaAte="2026-09-15T12:00:00Z" excedente={0} />);
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("gracaAte definido: mostra excedente e data limite formatada", () => {
    render(<GracaLimiteBanner gracaAte="2026-09-15T12:00:00Z" excedente={4} />);
    expect(
      screen.getByText(/Faltam inativar 4 aluno\(s\) até 15\/09\/2026/),
    ).toBeInTheDocument();
  });

  it("re-render com excedente diferente atualiza a contagem exibida", () => {
    const { rerender } = render(<GracaLimiteBanner gracaAte="2026-09-15T12:00:00Z" excedente={4} />);
    expect(screen.getByText(/Faltam inativar 4 aluno\(s\)/)).toBeInTheDocument();

    rerender(<GracaLimiteBanner gracaAte="2026-09-15T12:00:00Z" excedente={1} />);
    expect(screen.getByText(/Faltam inativar 1 aluno\(s\)/)).toBeInTheDocument();
    expect(screen.queryByText(/Faltam inativar 4 aluno\(s\)/)).not.toBeInTheDocument();
  });

  it("mostra link 'Ver planos' apontando para /treinador/plano", () => {
    render(<GracaLimiteBanner gracaAte="2026-09-15T12:00:00Z" excedente={4} />);
    const link = screen.getByRole("link", { name: "Ver planos" });
    expect(link).toHaveAttribute("href", "/treinador/plano");
  });
});
