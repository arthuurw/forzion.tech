import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import PagamentosPage from "../page";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), replace: vi.fn(), back: vi.fn() })),
}));

afterEach(() => server.resetHandlers());

describe("PagamentosAlunoPage — erro de carregamento", () => {
  it("listarPagamentosAssinatura falha → exibe detail do backend (não o fallback genérico)", async () => {
    server.use(
      http.get("*/aluno/assinatura", () =>
        HttpResponse.json({ assinaturaAlunoId: "as1" }),
      ),
      http.get("*/aluno/pagamentos/assinatura/as1", () =>
        HttpResponse.json(
          { detail: "Não foi possível listar os pagamentos desta assinatura." },
          { status: 400 },
        ),
      ),
    );

    render(<PagamentosPage />);

    await waitFor(() => {
      expect(
        screen.getByText("Não foi possível listar os pagamentos desta assinatura."),
      ).toBeInTheDocument();
    });
    expect(screen.queryByText("Erro ao carregar pagamentos.")).not.toBeInTheDocument();
  });
});
