import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type { TreinoAlunoDetalheResponse } from "@/lib/api/aluno";

const mockPush = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: mockPush, back: vi.fn(), replace: vi.fn() })),
}));

const FICHA_ATIVA: TreinoAlunoDetalheResponse = {
  treinoAlunoId: "f1",
  treinoId: "t1",
  nomeTreino: "Treino A",
  objetivo: "Hipertrofia",
  status: "Ativo",
  exercicios: [
    { treinoExercicioId: "e1", exercicioId: "ex1", nomeExercicio: "Supino", series: [], ordem: 1 },
    { treinoExercicioId: "e2", exercicioId: "ex2", nomeExercicio: "Agachamento", series: [], ordem: 2 },
  ],
};

const FICHA_INATIVA: TreinoAlunoDetalheResponse = {
  treinoAlunoId: "f2",
  treinoId: "t2",
  nomeTreino: "Treino B",
  objetivo: "Emagrecimento",
  status: "Inativo",
  exercicios: [],
};

async function renderLista(items: TreinoAlunoDetalheResponse[]) {
  server.use(
    http.get("*/aluno/fichas", () =>
      HttpResponse.json({ items, total: items.length, pagina: 1, tamanhoPagina: 10 })),
  );
  const { default: Page } = await import("@/app/(aluno)/aluno/fichas/page");
  render(<Page />);
  await waitFor(() => {
    expect(screen.queryByRole("progressbar")).not.toBeInTheDocument();
  });
}

describe("FichasAlunoPage — lista carregada", () => {
  afterEach(() => vi.clearAllMocks());

  it("exibe nome, objetivo e status de cada ficha carregada", async () => {
    await renderLista([FICHA_ATIVA, FICHA_INATIVA]);

    expect(screen.getByText("Treino A")).toBeInTheDocument();
    expect(screen.getByText("Hipertrofia")).toBeInTheDocument();
    expect(screen.getByLabelText("Status: Ativo")).toBeInTheDocument();

    expect(screen.getByText("Treino B")).toBeInTheDocument();
    expect(screen.getByText("Emagrecimento")).toBeInTheDocument();
    expect(screen.getByLabelText("Status: Inativo")).toBeInTheDocument();
  });

  it("clicar na linha da ficha navega para o detalhe", async () => {
    await renderLista([FICHA_ATIVA, FICHA_INATIVA]);

    fireEvent.click(screen.getByText("Treino A"));

    expect(mockPush).toHaveBeenCalledWith("/aluno/fichas/f1");
  });

  it("clicar no botão 'Ver ficha' navega para o detalhe", async () => {
    await renderLista([FICHA_ATIVA, FICHA_INATIVA]);

    fireEvent.click(screen.getAllByRole("button", { name: "Ver ficha" })[0]);

    expect(mockPush).toHaveBeenCalledWith("/aluno/fichas/f1");
  });

  it("ficha com status Ativo mostra 'Iniciar treino' que navega para a execução", async () => {
    await renderLista([FICHA_ATIVA, FICHA_INATIVA]);

    const iniciar = screen.getByRole("button", { name: "Iniciar treino" });
    fireEvent.click(iniciar);

    expect(mockPush).toHaveBeenCalledWith("/aluno/fichas/f1/executar");
  });

  it("ficha com status Inativo não mostra o botão 'Iniciar treino'", async () => {
    await renderLista([FICHA_ATIVA, FICHA_INATIVA]);

    expect(screen.getAllByRole("button", { name: "Iniciar treino" })).toHaveLength(1);
  });

  it("sem fichas cadastradas exibe a mensagem de estado vazio", async () => {
    await renderLista([]);

    expect(
      screen.getByText("Nenhum protocolo de treino disponível. Quem te treina ainda não vinculou fichas à sua conta."),
    ).toBeInTheDocument();
  });
});
