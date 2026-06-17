import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";

const captureException = vi.fn();
vi.mock("@sentry/nextjs", () => ({ captureException: (e: unknown) => captureException(e) }));

import AlunoError from "@/app/(aluno)/error";
import TreinadorError from "@/app/(treinador)/error";
import AdminError from "@/app/(admin)/error";

describe.each([
  ["aluno", AlunoError, "/aluno"],
  ["treinador", TreinadorError, "/treinador"],
  ["admin", AdminError, "/admin"],
])("error boundary do grupo %s", (_label, Boundary, painelHref) => {
  beforeEach(() => captureException.mockClear());

  it("reporta ao Sentry, mostra mensagem pt-BR e aciona reset", () => {
    const reset = vi.fn();
    const error = Object.assign(new Error("boom"), { digest: "d1" });

    render(<Boundary error={error} reset={reset} />);

    expect(captureException).toHaveBeenCalledWith(error);
    expect(screen.getByText("Não foi possível carregar esta página")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /painel/i })).toHaveAttribute("href", painelHref);

    fireEvent.click(screen.getByRole("button", { name: "Tentar novamente" }));
    expect(reset).toHaveBeenCalledTimes(1);
  });
});
