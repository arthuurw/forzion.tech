import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import PageHeader from "./PageHeader";

describe("PageHeader", () => {
  it("renderiza título como h1 por padrão", () => {
    render(<PageHeader title="Meus alunos" />);
    expect(screen.getByRole("heading", { level: 1, name: "Meus alunos" })).toBeInTheDocument();
  });

  it("usa elemento semântico de as quando informado", () => {
    render(<PageHeader title="Seção" as="h2" />);
    expect(screen.getByRole("heading", { level: 2, name: "Seção" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { level: 1 })).not.toBeInTheDocument();
  });

  it("título usa variant h5 do tema (peso vem do token, sem override inline)", () => {
    render(<PageHeader title="Título" />);
    expect(screen.getByRole("heading", { name: "Título" })).toHaveClass("MuiTypography-h5");
  });

  it("renderiza subtítulo quando informado e omite quando ausente", () => {
    const { rerender } = render(<PageHeader title="X" subtitle="linha secundária" />);
    expect(screen.getByText("linha secundária")).toBeInTheDocument();
    rerender(<PageHeader title="X" />);
    expect(screen.queryByText("linha secundária")).not.toBeInTheDocument();
  });

  it("posiciona a ação informada", () => {
    render(<PageHeader title="X" action={<button>Novo</button>} />);
    expect(screen.getByRole("button", { name: "Novo" })).toBeInTheDocument();
  });

  it("renderiza 'Voltar' apontando para backHref e omite quando ausente", () => {
    const { rerender } = render(<PageHeader title="X" backHref="/treinador/alunos" />);
    const voltar = screen.getByRole("link", { name: /voltar/i });
    expect(voltar).toHaveAttribute("href", "/treinador/alunos");
    rerender(<PageHeader title="X" />);
    expect(screen.queryByRole("link", { name: /voltar/i })).not.toBeInTheDocument();
  });
});
