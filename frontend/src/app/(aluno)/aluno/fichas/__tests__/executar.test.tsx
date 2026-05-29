/**
 * Testes para ExecutarFichaPage.
 * Cobre: exibição do hint de agregação por exercício (Bug 4).
 *
 * Endpoint:
 *   GET /aluno/fichas/:id  -> alunoApi.getFicha
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type { TreinoExercicioResponse, SerieConfigResponse } from "@/types";
import type { TreinoAlunoDetalheResponse } from "@/lib/api/aluno";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), replace: vi.fn(), back: vi.fn() })),
  useParams: vi.fn(() => ({ fichaId: "ficha-1" })),
}));

// ─── Factories ───────────────────────────────────────────────────────────────

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

// ─── Imports (lazy após mocks) ────────────────────────────────────────────────

import ExecutarFichaPage from "../[fichaId]/executar/page";

// ─── Tests ───────────────────────────────────────────────────────────────────

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

    // Espera a ficha carregar
    await screen.findByText("Supino");

    // O span com title contendo "média" deve existir
    const infoIcon = document.querySelector('[title*="média"]');
    expect(infoIcon).not.toBeNull();
  });
});
