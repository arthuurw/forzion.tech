/**
 * Testes para ExecutarFichaPage.
 * Cobre: exibição do hint de agregação por exercício (Bug 4).
 *
 * Endpoint:
 *   GET /aluno/fichas/:id  -> alunoApi.getFicha
 */
import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type { TreinoExercicioResponse, SerieConfigResponse } from "@/types";
import type { TreinoAlunoDetalheResponse } from "@/lib/api/aluno";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), replace: vi.fn(), back: vi.fn() })),
  useParams: vi.fn(() => ({ fichaId: "ficha-1" })),
}));

vi.mock("@/lib/auth/context", () => ({
  useAuth: () => ({
    user: { contaId: "c1", perfilId: "aluno-1", tipoConta: "Aluno", nome: "Aluno" },
    isLoading: false,
    login: vi.fn(),
    logout: vi.fn(),
  }),
}));

const DRAFT_KEY = "exec-draft:aluno-1:ficha-1";

function seedDraft(execData: Record<string, { reps: string; carga: string }[]>, observacao = "nota restaurada") {
  localStorage.setItem(
    DRAFT_KEY,
    JSON.stringify({
      v: 1,
      idempotencyKey: "11111111-1111-1111-1111-111111111111",
      treinoExercicioIds: Object.keys(execData),
      execData,
      obsData: {},
      observacao,
      currentIndex: 0,
      updatedAt: Date.now(),
    }),
  );
}

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
  beforeEach(() => localStorage.clear());
  afterEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

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
    // eslint-disable-next-line testing-library/no-node-access
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

    expect(await screen.findByText(/não tem um vínculo ativo/i)).toBeInTheDocument();
  });

  it("registro inválido (400) exibe a mensagem do servidor, não o genérico", async () => {
    respondFicha();
    server.use(
      http.post("*/aluno/execucoes", () =>
        HttpResponse.json(
          { detail: "A observação deve ter no máximo 500 caracteres." },
          { status: 400 },
        ),
      ),
    );
    render(<ExecutarFichaPage />);

    await screen.findByText("Supino");
    fireEvent.click(screen.getByRole("button", { name: /Finalizar treino/ }));
    fireEvent.click(await screen.findByRole("button", { name: /Confirmar registro/ }));

    expect(
      await screen.findByText("A observação deve ter no máximo 500 caracteres."),
    ).toBeInTheDocument();
    expect(screen.queryByText(/Tente novamente/i)).not.toBeInTheDocument();
  });

  it("draft existente exibe banner de restauração e Continuar aplica o estado salvo (EXOFF-02)", async () => {
    seedDraft({ "ex-1": [{ reps: "99", carga: "50" }] });
    respondFicha();
    render(<ExecutarFichaPage />);

    await screen.findByText("Supino");
    expect(await screen.findByText(/Treino em andamento encontrado/i)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Continuar" }));
    expect(await screen.findByDisplayValue("99")).toBeInTheDocument();
  });

  it("Descartar remove o draft e fecha o banner (EXOFF-05)", async () => {
    seedDraft({ "ex-1": [{ reps: "99", carga: "50" }] });
    respondFicha();
    render(<ExecutarFichaPage />);

    await screen.findByText(/Treino em andamento encontrado/i);
    fireEvent.click(screen.getByRole("button", { name: "Descartar" }));

    await waitFor(() =>
      expect(screen.queryByText(/Treino em andamento encontrado/i)).not.toBeInTheDocument(),
    );
    expect(localStorage.getItem(DRAFT_KEY)).toBeNull();
  });

  it("falha de rede ao finalizar enfileira e mostra 'salva no aparelho' (EXOFF-11/12)", async () => {
    respondFicha();
    server.use(http.post("*/aluno/execucoes", () => HttpResponse.error()));
    render(<ExecutarFichaPage />);

    await screen.findByText("Supino");
    fireEvent.click(screen.getByRole("button", { name: /Finalizar treino/ }));
    fireEvent.click(await screen.findByRole("button", { name: /Confirmar registro/ }));

    expect(await screen.findByText(/salva no aparelho/i)).toBeInTheDocument();
    const queue = JSON.parse(localStorage.getItem("exec-queue")!);
    expect(queue).toHaveLength(1);
    expect(queue[0].idempotencyKey).toBeTruthy();
    expect(queue[0].treinoId).toBe("treino-1");
  });

  it("sucesso ao finalizar limpa o draft (EXOFF-05)", async () => {
    respondFicha();
    server.use(
      http.post("*/aluno/execucoes", () =>
        HttpResponse.json({ execucaoId: "e1", treinoId: "treino-1" }, { status: 201 }),
      ),
    );
    render(<ExecutarFichaPage />);

    await screen.findByText("Supino");
    await waitFor(() => expect(localStorage.getItem(DRAFT_KEY)).not.toBeNull());

    fireEvent.click(screen.getByRole("button", { name: /Finalizar treino/ }));
    fireEvent.click(await screen.findByRole("button", { name: /Confirmar registro/ }));

    expect(await screen.findByText("Sessão registrada")).toBeInTheDocument();
    expect(localStorage.getItem(DRAFT_KEY)).toBeNull();
  });
});
