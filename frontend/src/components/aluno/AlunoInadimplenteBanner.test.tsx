import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import AlunoInadimplenteBanner from "./AlunoInadimplenteBanner";

vi.mock("next/link", () => ({
  default: ({ href, children }: { href: string; children: React.ReactNode }) => (
    <a href={href}>{children}</a>
  ),
}));

describe("AlunoInadimplenteBanner", () => {
  it("renderiza Alert com role=alert e mensagem principal", () => {
    render(<AlunoInadimplenteBanner />);
    const alert = screen.getByRole("alert");
    expect(alert).toBeInTheDocument();
    expect(alert).toHaveTextContent(/Regularize seu pagamento/i);
  });

  it("exibe titulo 'Assinatura inadimplente'", () => {
    render(<AlunoInadimplenteBanner />);
    expect(screen.getByText("Assinatura inadimplente")).toBeInTheDocument();
  });

  it("renderiza CTA 'Regularizar agora' apontando para /aluno/pagamentos", () => {
    render(<AlunoInadimplenteBanner />);
    const link = screen.getByRole("link", { name: /Regularizar agora/i });
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute("href", "/aluno/pagamentos");
  });

  it("variant default = warning (MuiAlert-standardWarning class)", () => {
    render(<AlunoInadimplenteBanner />);
    const alert = screen.getByRole("alert");
    expect(alert.className).toMatch(/Warning/);
  });

  it("variant error aplica classe MuiAlert-standardError", () => {
    render(<AlunoInadimplenteBanner variant="error" />);
    const alert = screen.getByRole("alert");
    expect(alert.className).toMatch(/Error/);
  });

  it("nao expoe botao de fechar (banner persistente)", () => {
    render(<AlunoInadimplenteBanner />);
    expect(screen.queryByRole("button", { name: /close/i })).not.toBeInTheDocument();
  });
});
