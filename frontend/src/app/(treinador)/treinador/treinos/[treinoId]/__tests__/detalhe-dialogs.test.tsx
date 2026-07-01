import React from "react";
import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type {
  TreinoResponse,
  TreinoExercicioResponse,
  ExercicioResponse,
  AlunoResponse,
  TreinoAlunoVinculado,
} from "@/types";

const mockPush = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: mockPush, back: vi.fn(), replace: vi.fn() })),
  useParams: vi.fn(() => ({ treinoId: "treino-1" })),
}));

afterEach(() => {
  vi.clearAllMocks();
  vi.useRealTimers();
});

const SERIE_A = {
  serieConfigId: "sc-1",
  quantidade: 3,
  repeticoesMin: 10,
  repeticoesMax: 12,
  descricao: null,
  carga: 40,
  descanso: 60,
  ordem: 1,
};

const SERIE_B = {
  serieConfigId: "sc-2",
  quantidade: 4,
  repeticoesMin: 8,
  repeticoesMax: 10,
  descricao: null,
  carga: 50,
  descanso: 90,
  ordem: 2,
};

const SERIE_C = {
  serieConfigId: "sc-3",
  quantidade: 3,
  repeticoesMin: 12,
  repeticoesMax: null,
  descricao: null,
  carga: null,
  descanso: 45,
  ordem: 1,
};

const EX_SUPINO: TreinoExercicioResponse = {
  treinoExercicioId: "te-1",
  exercicioId: "ex-1",
  nomeExercicio: "Supino",
  series: [SERIE_A, SERIE_B],
  ordem: 1,
  observacao: "Manter postura ereta",
};

const EX_AGACH: TreinoExercicioResponse = {
  treinoExercicioId: "te-2",
  exercicioId: "ex-2",
  nomeExercicio: "Agachamento",
  series: [SERIE_C],
  ordem: 2,
  observacao: null,
};

function buildFicha(exercicios: TreinoExercicioResponse[]): TreinoResponse {
  return {
    treinoId: "treino-1",
    nome: "Treino A",
    objetivo: "Hipertrofia",
    dificuldade: "Iniciante",
    dataInicio: null,
    dataFim: null,
    treinadorId: "t-1",
    exercicios,
    createdAt: "2024-01-01T00:00:00Z",
    updatedAt: null,
  };
}

const FICHA_COM_EX = buildFicha([EX_SUPINO, EX_AGACH]);
const FICHA_VAZIA = buildFicha([]);

const EX_NOVO: ExercicioResponse = {
  exercicioId: "ex-9",
  nome: "Remada curvada",
  descricao: null,
  grupoMuscularId: "gm-9",
  grupoMuscular: "Costas",
  treinadorId: "t-1",
  isGlobal: false,
};

const ALUNO_JOAO: AlunoResponse = {
  alunoId: "a-1",
  nome: "João",
  email: "joao@example.com",
  telefone: null,
  status: "Ativo",
  contaId: "c-1",
  createdAt: "2024-01-01T00:00:00Z",
  updatedAt: null,
  diasDisponiveis: null,
  tempoDisponivelMinutos: null,
  finalidade: null,
  focoTreino: null,
  nivelCondicionamento: null,
  limitacoesFisicas: null,
  doencas: null,
  observacoesAdicionais: null,
};

function paginatedExercicios(items: ExercicioResponse[]) {
  return { items, total: items.length, pagina: 1, tamanhoPagina: 20 };
}

function paginatedAlunos(items: AlunoResponse[]) {
  return { items, total: items.length, pagina: 1, tamanhoPagina: 200 };
}

async function renderPage(ficha: TreinoResponse | null) {
  server.use(
    http.get("*/treinos/treino-1", () =>
      ficha ? HttpResponse.json(ficha) : HttpResponse.json({}, { status: 500 }),
    ),
  );
  const { default: Page } = await import(
    "@/app/(treinador)/treinador/treinos/[treinoId]/page"
  );
  render(<Page />);
  await waitFor(() => {
    expect(screen.queryByRole("progressbar")).not.toBeInTheDocument();
  });
}

function getDialog() {
  return screen.getByRole("dialog");
}

describe("DetalheFichaPage — Adicionar exercício", () => {
  it("botão Adicionar fica desabilitado quando o campo Qtd séries é esvaziado, mesmo com exercício selecionado", async () => {
    await renderPage(FICHA_COM_EX);
    server.use(
      http.get("*/treinador/exercicios", () => HttpResponse.json(paginatedExercicios([EX_NOVO]))),
    );

    fireEvent.click(screen.getByRole("button", { name: "Adicionar exercício" }));
    const dialog = getDialog();
    const input = within(dialog).getByRole("combobox");
    fireEvent.change(input, { target: { value: "remada" } });

    const option = await screen.findByRole("option", { name: /Remada curvada/i }, { timeout: 1000 });
    fireEvent.click(option);

    await waitFor(() => {
      expect(within(dialog).getByRole("button", { name: "Adicionar" })).toBeEnabled();
    });

    fireEvent.change(within(dialog).getByLabelText("Qtd séries"), { target: { value: "" } });

    expect(within(dialog).getByRole("button", { name: "Adicionar" })).toBeDisabled();
  });
});

describe("DetalheFichaPage — Remover exercício", () => {
  it("confirma remoção via ConfirmDialog, envia DELETE e mostra banner de sucesso", async () => {
    await renderPage(FICHA_COM_EX);
    let deletedPath = "";
    server.use(
      http.delete("*/treinos/treino-1/exercicios/te-2", ({ request }) => {
        deletedPath = new URL(request.url).pathname;
        return HttpResponse.json({});
      }),
      http.get("*/treinos/treino-1", () => HttpResponse.json(buildFicha([EX_SUPINO]))),
    );

    fireEvent.click(screen.getAllByRole("button", { name: "Remover exercício" })[1]);

    const dialog = getDialog();
    expect(within(dialog).getByText(/Remover "Agachamento" desta ficha\?/i)).toBeInTheDocument();

    fireEvent.click(within(dialog).getByRole("button", { name: "Remover" }));

    await waitFor(() => {
      expect(screen.getByText("Exercício removido.")).toBeInTheDocument();
    });
    expect(deletedPath).toContain("/treinos/treino-1/exercicios/te-2");
  });
});

describe("DetalheFichaPage — Duplicar ficha", () => {
  it("duplica com sucesso e navega para a nova ficha", async () => {
    await renderPage(FICHA_COM_EX);
    server.use(
      http.post("*/treinos/treino-1/duplicar", () =>
        HttpResponse.json({ ...FICHA_COM_EX, treinoId: "treino-99" }),
      ),
    );

    fireEvent.click(screen.getByRole("button", { name: "Duplicar" }));

    await waitFor(() => {
      expect(mockPush).toHaveBeenCalledWith("/treinador/treinos/treino-99");
    });
  });

  it("erro na duplicação mostra banner e reabilita o botão", async () => {
    await renderPage(FICHA_COM_EX);
    server.use(
      http.post("*/treinos/treino-1/duplicar", () => HttpResponse.json({}, { status: 500 })),
    );

    const button = screen.getByRole("button", { name: "Duplicar" });
    fireEvent.click(button);

    await waitFor(() => {
      expect(screen.getByText("Erro ao duplicar ficha.")).toBeInTheDocument();
    });
    expect(button).toBeEnabled();
    expect(mockPush).not.toHaveBeenCalled();
  });
});

describe("DetalheFichaPage — Vincular aluno", () => {
  it("aluno já vinculado gera erro e não abre o diálogo", async () => {
    await renderPage(FICHA_COM_EX);
    const jaVinculado: TreinoAlunoVinculado[] = [
      { treinoAlunoId: "ta-1", alunoId: "a-1", nomeAluno: "Maria", status: "Ativo" },
    ];
    server.use(
      http.get("*/treinos/treino-1/alunos", () => HttpResponse.json(jaVinculado)),
    );

    fireEvent.click(screen.getByRole("button", { name: "Vincular aluno" }));

    await waitFor(() => {
      expect(
        screen.getByText("Já existe o aluno Maria vinculado a esta ficha."),
      ).toBeInTheDocument();
    });
    expect(screen.queryByText("Vincular ficha a aluno")).not.toBeInTheDocument();
  });

  it("sem vínculo existente, abre diálogo, carrega alunos ativos sob demanda e vincula", async () => {
    await renderPage(FICHA_COM_EX);
    let vinculouPath = "";
    server.use(
      http.get("*/treinos/treino-1/alunos", () => HttpResponse.json([])),
      http.get("*/treinador/alunos", () => HttpResponse.json(paginatedAlunos([ALUNO_JOAO]))),
      http.post("*/treinador/alunos/a-1/fichas/treino-1", ({ request }) => {
        vinculouPath = new URL(request.url).pathname;
        return HttpResponse.json({});
      }),
    );

    fireEvent.click(screen.getByRole("button", { name: "Vincular aluno" }));

    const dialog = await screen.findByRole("dialog");
    within(dialog).getByText("Vincular ficha a aluno");

    const input = within(dialog).getByRole("combobox");
    fireEvent.change(input, { target: { value: "Jo" } });
    const option = await screen.findByRole("option", { name: /João/i });
    fireEvent.click(option);

    fireEvent.click(within(dialog).getByRole("button", { name: "Vincular" }));

    await waitFor(() => {
      expect(screen.getByText("Ficha vinculada a João.")).toBeInTheDocument();
    });
    expect(vinculouPath).toContain("/treinador/alunos/a-1/fichas/treino-1");
    await waitFor(() => {
      expect(screen.queryByText("Vincular ficha a aluno")).not.toBeInTheDocument();
    });
  });
});

describe("DetalheFichaPage — Editar ficha", () => {
  it("pré-preenche nome/objetivo, desabilita Salvar sem nome e envia PATCH ao salvar", async () => {
    await renderPage(FICHA_COM_EX);
    let patchBody: Record<string, unknown> | null = null;
    server.use(
      http.patch("*/treinos/treino-1", async ({ request }) => {
        patchBody = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(FICHA_COM_EX);
      }),
      http.get("*/treinos/treino-1", () => HttpResponse.json(FICHA_COM_EX)),
    );

    fireEvent.click(screen.getByRole("button", { name: "Editar" }));
    const dialog = getDialog();
    const nomeInput = within(dialog).getByLabelText(/^Nome/) as HTMLInputElement;
    expect(nomeInput.value).toBe("Treino A");

    fireEvent.change(nomeInput, { target: { value: "" } });
    expect(within(dialog).getByRole("button", { name: "Salvar" })).toBeDisabled();

    fireEvent.change(nomeInput, { target: { value: "Treino Renovado" } });
    fireEvent.click(within(dialog).getByRole("button", { name: "Salvar" }));

    await waitFor(() => {
      expect(screen.getByText("Ficha atualizada.")).toBeInTheDocument();
    });
    expect(patchBody).toEqual({ nome: "Treino Renovado", objetivo: "Hipertrofia" });
  });
});

describe("DetalheFichaPage — Editar séries", () => {
  it("quantidade vazia em uma das séries é coagida para 1 no PUT, com a outra série válida mantendo o botão habilitado", async () => {
    await renderPage(FICHA_COM_EX);
    let putBody: Record<string, unknown> | null = null;
    server.use(
      http.put("*/treinos/treino-1/exercicios/te-1", async ({ request }) => {
        putBody = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(FICHA_COM_EX);
      }),
      http.get("*/treinos/treino-1", () => HttpResponse.json(FICHA_COM_EX)),
    );

    fireEvent.click(screen.getAllByRole("button", { name: "Editar séries" })[0]);
    const dialog = getDialog();
    within(dialog).getByText("Editar séries — Supino");

    const qtdInputs = within(dialog).getAllByLabelText("Qtd séries");
    expect(qtdInputs).toHaveLength(2);
    fireEvent.change(qtdInputs[1], { target: { value: "" } });

    expect(within(dialog).getByRole("button", { name: "Salvar" })).toBeEnabled();
    fireEvent.click(within(dialog).getByRole("button", { name: "Salvar" }));

    await waitFor(() => {
      expect(screen.getByText("Exercício atualizado.")).toBeInTheDocument();
    });
    const series = (putBody as unknown as { series: Array<{ quantidade: number }> }).series;
    expect(series[0].quantidade).toBe(3);
    expect(series[1].quantidade).toBe(1);
  });
});

describe("DetalheFichaPage — Observação do exercício", () => {
  it("exercício com observação existente abre em modo edição, permite Limpar e salva observacao null", async () => {
    await renderPage(FICHA_COM_EX);
    let patchBody: Record<string, unknown> | null = null;
    server.use(
      http.patch("*/treinos/treino-1/exercicios/te-1/observacao", async ({ request }) => {
        patchBody = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(FICHA_COM_EX);
      }),
      http.get("*/treinos/treino-1", () => HttpResponse.json(FICHA_COM_EX)),
    );

    fireEvent.click(screen.getAllByRole("button", { name: "Editar observação" })[0]);
    const dialog = getDialog();
    within(dialog).getByText("Editar observação");
    const obsInput = within(dialog).getByLabelText(/Observação/i) as HTMLInputElement;
    expect(obsInput.value).toBe("Manter postura ereta");

    fireEvent.click(within(dialog).getByRole("button", { name: "Limpar" }));
    expect(obsInput.value).toBe("");

    fireEvent.click(within(dialog).getByRole("button", { name: "Salvar" }));

    await waitFor(() => {
      expect(screen.getByText("Observação salva.")).toBeInTheDocument();
    });
    expect(patchBody).toEqual({ observacao: null });
  });

  it("exercício sem observação abre em modo adicionar, sem botão Limpar, e salva texto novo", async () => {
    await renderPage(FICHA_COM_EX);
    let patchBody: Record<string, unknown> | null = null;
    server.use(
      http.patch("*/treinos/treino-1/exercicios/te-2/observacao", async ({ request }) => {
        patchBody = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(FICHA_COM_EX);
      }),
      http.get("*/treinos/treino-1", () => HttpResponse.json(FICHA_COM_EX)),
    );

    fireEvent.click(screen.getAllByRole("button", { name: "Adicionar observação" })[0]);
    const dialog = getDialog();
    within(dialog).getByText("Adicionar observação");
    expect(within(dialog).queryByRole("button", { name: "Limpar" })).not.toBeInTheDocument();

    fireEvent.change(within(dialog).getByLabelText(/Observação/i), {
      target: { value: "Cuidado com o joelho" },
    });
    fireEvent.click(within(dialog).getByRole("button", { name: "Salvar" }));

    await waitFor(() => {
      expect(screen.getByText("Observação salva.")).toBeInTheDocument();
    });
    expect(patchBody).toEqual({ observacao: "Cuidado com o joelho" });
  });
});

describe("DetalheFichaPage — Excluir ficha", () => {
  it("confirma exclusão e navega para a listagem", async () => {
    await renderPage(FICHA_COM_EX);
    server.use(http.delete("*/treinos/treino-1", () => HttpResponse.json({})));

    fireEvent.click(screen.getByRole("button", { name: "Excluir" }));
    const dialog = getDialog();
    fireEvent.click(within(dialog).getByRole("button", { name: "Excluir" }));

    await waitFor(() => {
      expect(mockPush).toHaveBeenCalledWith("/treinador/treinos");
    });
  });

  it("erro ao excluir mostra banner sobre execuções registradas e fecha o diálogo", async () => {
    await renderPage(FICHA_COM_EX);
    server.use(http.delete("*/treinos/treino-1", () => HttpResponse.json({}, { status: 500 })));

    fireEvent.click(screen.getByRole("button", { name: "Excluir" }));
    const dialog = getDialog();
    fireEvent.click(within(dialog).getByRole("button", { name: "Excluir" }));

    await waitFor(() => {
      expect(
        screen.getByText(
          "Erro ao excluir ficha. Fichas com execuções registradas não podem ser excluídas.",
        ),
      ).toBeInTheDocument();
    });
    await waitFor(() => {
      expect(screen.queryByText("Excluir ficha")).not.toBeInTheDocument();
    });
    expect(mockPush).not.toHaveBeenCalled();
  });
});

describe("DetalheFichaPage — carregamento com erro", () => {
  it("erro ao buscar a ficha renderiza DetalheErro com retry/voltar", async () => {
    await renderPage(null);

    expect(screen.getByText("Erro ao carregar ficha.")).toBeInTheDocument();
    const voltar = screen.getByRole("button", { name: "Voltar" });
    fireEvent.click(voltar);
    expect(mockPush).toHaveBeenCalledWith("/treinador/treinos");
  });
});

describe("DetalheFichaPage — ficha sem exercícios", () => {
  it("mostra EmptyState e a ação abre o diálogo de adicionar exercício", async () => {
    await renderPage(FICHA_VAZIA);

    expect(screen.getByText("Nenhum exercício nesta ficha ainda.")).toBeInTheDocument();
    const botoesAdicionar = screen.getAllByRole("button", { name: "Adicionar exercício" });
    expect(botoesAdicionar.length).toBeGreaterThan(1);

    fireEvent.click(botoesAdicionar[1]);

    await waitFor(() => {
      expect(screen.getByRole("dialog")).toBeInTheDocument();
    });
  });
});
