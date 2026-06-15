/**
 * Testes para ExecutarFichaPage.
 * Cobre: exibição do hint de agregação por exercício (Bug 4).
 *
 * Endpoint:
 *   GET /aluno/fichas/:id  -> alunoApi.getFicha
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type { TreinoExercicioResponse, SerieConfigResponse } from "@/types";
import type { TreinoAlunoDetalheResponse } from "@/lib/api/aluno";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), replace: vi.fn(), back: vi.fn() })),
  useParams: vi.fn(() => ({ fichaId: "ficha-1" })),
}));

function makeSerie(overrides: Partial<SerieConfigResponse> = {}): SerieConfigResponse {
  return {
    serieConfigId: "s-1",
    quantidade: 3,
    repeticoesMin: 10,
    repeticoesMax: null,
    descricao: null,
    carga: 20,
    descanso: 60,
    ordem: 1,
    ...overrides,
  };
}

function makeExercicio(
  overrides: Partial<TreinoExercicioResponse> = {},
): TreinoExercicioResponse {
  return {
    treinoExercicioId: "ex-1",
    exercicioId: "ex-base-1",
    nomeExercicio: "Supino",
    series: [makeSerie()],
    ordem: 1,
    observacao: null,
    ...overrides,
  };
}

function makeFicha(
  overrides: Partial<TreinoAlunoDetalheResponse> = {},
): TreinoAlunoDetalheResponse {
  return {
    treinoAlunoId: "ficha-1",
    treinoId: "treino-1",
    nomeTreino: "Treino A",
    objetivo: "Hipertrofia",
    status: "Ativo",
    exercicios: [makeExercicio()],
    ...overrides,
  };
}

function respondFicha(ficha: TreinoAlunoDetalheResponse = makeFicha()) {
  server.use(
    http.get("*/aluno/fichas/:id", () => HttpResponse.json(ficha)),
  );
}

// Import lazy após mocks.
import ExecutarFichaPage from "../[fichaId]/executar/page";

describe("ExecutarFichaPage — hint de agregação por exercício", () => {
  afterEach(() => vi.clearAllMocks());

  // Bug 4: a UI deve deixar claro que reps/carga são registrados como média
  // das séries, pois o payload da API armazena um único valor por exercício.
  it("exibe o hint de agregação ao renderizar exercício com séries", async () => {
    respondFicha();
    render(<ExecutarFichaPage />);

    const hint = await screen.findByTestId("exec-aggregation-hint");
    expect(hint).toBeInTheDocument();
    expect(hint).toHaveTextContent(/m(é|e)dia/i);
  });

  it("tooltip do ícone de info está presente na seção 'Executado'", async () => {
    respondFicha();
    render(<ExecutarFichaPage />);

    await screen.findByText("Supino");

    // O span com title contendo "média" deve existir
    const infoIcon = document.querySelector('[title*="média"]');
    expect(infoIcon).not.toBeNull();
  });

  // R6 — dots de progresso têm alvo de toque acessível (ButtonBase com aria-label)
  // e navegam ao exercício correspondente.
  it("dots de progresso: botão com aria-label navega ao exercício (R6)", async () => {
    respondFicha(
      makeFicha({
        exercicios: [
          makeExercicio({ treinoExercicioId: "ex-1", nomeExercicio: "Supino" }),
          makeExercicio({ treinoExercicioId: "ex-2", nomeExercicio: "Agachamento" }),
        ],
      }),
    );
    render(<ExecutarFichaPage />);
    await screen.findByText("Supino");

    const dot2 = screen.getByRole("button", { name: "Ir para exercício 2" });
    expect(dot2).toBeInTheDocument();
    fireEvent.click(dot2);
    expect(await screen.findByText("Agachamento")).toBeInTheDocument();
  });

  it("registro sem treinador ativo (403) exibe mensagem clara de bloqueio", async () => {
    respondFicha();
    server.use(
      http.post("*/aluno/execucoes", () =>
        HttpResponse.json({ status: 403, title: "Acesso negado" }, { status: 403 }),
      ),
    );
    render(<ExecutarFichaPage />);

    await screen.findByText("Supino");
    fireEvent.click(screen.getByRole("button", { name: /Finalizar treino/ }));
    fireEvent.click(await screen.findByRole("button", { name: /Confirmar registro/ }));

    expect(await screen.findByText(/não tem um treinador ativo/i)).toBeInTheDocument();
  });
});
