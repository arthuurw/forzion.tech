import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type { TreinoAlunoDetalheResponse } from "@/lib/api/aluno";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), replace: vi.fn(), back: vi.fn() })),
  useParams: vi.fn(() => ({ fichaId: "ficha-1" })),
}));

function makeFicha(): TreinoAlunoDetalheResponse {
  return {
    treinoAlunoId: "ficha-1",
    treinoId: "treino-1",
    nomeTreino: "Treino A",
    objetivo: "Hipertrofia",
    status: "Ativo",
    exercicios: [],
  };
}

import DetalheFichaAlunoPage from "../[fichaId]/page";

describe("DetalheFichaAlunoPage — falha de load", () => {
  afterEach(() => vi.clearAllMocks());

  it("falha → estado de erro com retry (não tela branca)", async () => {
    server.use(
      http.get("*/aluno/fichas/:id", () => HttpResponse.json({}, { status: 500 })),
    );
    render(<DetalheFichaAlunoPage />);

    expect(await screen.findByText("Erro ao carregar ficha.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Tentar novamente" })).toBeInTheDocument();
  });

  it("retry re-chama o fetch e carrega a ficha", async () => {
    server.use(
      http.get("*/aluno/fichas/:id", () => HttpResponse.json({}, { status: 500 })),
    );
    render(<DetalheFichaAlunoPage />);

    const retry = await screen.findByRole("button", { name: "Tentar novamente" });
    server.use(
      http.get("*/aluno/fichas/:id", () => HttpResponse.json(makeFicha())),
    );
    fireEvent.click(retry);

    expect(await screen.findByText("Treino A")).toBeInTheDocument();
  });
});
