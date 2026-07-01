import React from "react";
import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, waitFor, fireEvent, within } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type { AlunoResponse, TreinoAlunoResponse, TreinoResponse } from "@/types";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn() })),
  useParams: vi.fn(() => ({ alunoId: "aluno-1" })),
}));

vi.mock("@/components/treinador/ProgressaoAluno", () => ({
  default: ({ alunoId }: { alunoId: string }) => (
    <div data-testid="progressao-aluno-stub">progressao-{alunoId}</div>
  ),
}));

const BASE_ALUNO: AlunoResponse = {
  alunoId: "aluno-1",
  nome: "João",
  email: null,
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
  pacoteId: null,
  pacoteNome: null,
};

const FICHA_VINCULADA: TreinoAlunoResponse = {
  treinoAlunoId: "ta-1",
  treinoId: "t-1",
  nomeTreino: "Treino A",
  status: "Ativo",
};

const FICHA_DISPONIVEL: TreinoResponse = {
  treinoId: "t-2",
  nome: "Treino Full",
  objetivo: "Hipertrofia",
  dificuldade: "Iniciante",
  dataInicio: null,
  dataFim: null,
  treinadorId: "trainer-1",
  exercicios: [],
  createdAt: "2024-01-01T00:00:00Z",
  updatedAt: null,
};

function paginatedFichas(items: TreinoResponse[]) {
  return { items, total: items.length, pagina: 1, tamanhoPagina: 100 };
}

async function renderPage(aluno: AlunoResponse, fichas: TreinoAlunoResponse[] = []) {
  server.use(
    http.get("*/treinador/alunos/aluno-1", () => HttpResponse.json(aluno)),
    http.get("*/treinador/alunos/aluno-1/fichas", () => HttpResponse.json(fichas)),
  );
  const { default: Page } = await import(
    "@/app/(treinador)/treinador/alunos/[alunoId]/page"
  );
  render(<Page />);
  await waitFor(() => {
    expect(screen.queryByRole("progressbar")).not.toBeInTheDocument();
  });
}

describe("DetalheAlunoPage — InfoLines condicionais e perfil de treino", () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it("exibe E-mail, Celular e Atualizado quando presentes no aluno", async () => {
    await renderPage({
      ...BASE_ALUNO,
      email: "joao@teste.com",
      telefone: "11987654321",
      updatedAt: "2024-02-01T00:00:00Z",
    });

    expect(screen.getByText(/E-mail:/)).toBeInTheDocument();
    expect(screen.getByText(/\(11\) 98765-4321/)).toBeInTheDocument();
    expect(screen.getByText(/Atualizado:/)).toBeInTheDocument();
  });

  it("oculta E-mail, Celular e Atualizado quando ausentes no aluno", async () => {
    await renderPage(BASE_ALUNO);

    expect(screen.queryByText(/E-mail:/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Celular:/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Atualizado:/)).not.toBeInTheDocument();
  });

  it("com campos de perfil de treino preenchidos, exibe o card Perfil de treino", async () => {
    await renderPage({
      ...BASE_ALUNO,
      finalidade: "Hipertrofia",
      nivelCondicionamento: "Iniciante",
      focoTreino: "Braços",
    });

    expect(screen.getByText("Perfil de treino")).toBeInTheDocument();
    expect(screen.getByText(/Finalidade:/)).toBeInTheDocument();
    expect(screen.getByText(/Nível:/)).toBeInTheDocument();
    expect(screen.getByText(/Foco:/)).toBeInTheDocument();
  });

  it("sem nenhum campo de perfil de treino, oculta o card Perfil de treino", async () => {
    await renderPage(BASE_ALUNO);

    expect(screen.queryByText("Perfil de treino")).not.toBeInTheDocument();
  });
});

describe("DetalheAlunoPage — status do vínculo", () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it("status AguardandoAprovacao exibe aviso e oculta Fichas vinculadas e ProgressaoAluno", async () => {
    await renderPage({ ...BASE_ALUNO, status: "AguardandoAprovacao" }, [FICHA_VINCULADA]);

    expect(screen.getByText(/Vínculo aguardando aprovação/i)).toBeInTheDocument();
    expect(screen.queryByText(/Fichas vinculadas/)).not.toBeInTheDocument();
    expect(screen.queryByTestId("progressao-aluno-stub")).not.toBeInTheDocument();
  });

  it("status Ativo sem fichas mostra EmptyState e renderiza ProgressaoAluno", async () => {
    await renderPage(BASE_ALUNO, []);

    expect(screen.getByText("Nenhuma ficha vinculada a este aluno.")).toBeInTheDocument();
    expect(screen.getByTestId("progressao-aluno-stub")).toHaveTextContent("progressao-aluno-1");
  });

  it("status Ativo com fichas renderiza a linha da tabela com nome e status", async () => {
    await renderPage(BASE_ALUNO, [FICHA_VINCULADA]);

    expect(screen.getByText("Fichas vinculadas (1)")).toBeInTheDocument();
    expect(screen.getByText("Treino A")).toBeInTheDocument();
  });
});

describe("DetalheAlunoPage — vincular ficha ao aluno", () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it("abrir o modal pela primeira vez busca as fichas disponíveis; reabrir não rebusca (guard)", async () => {
    let listFichasCalls = 0;
    server.use(
      http.get("*/treinador/treinos", () => {
        listFichasCalls++;
        return HttpResponse.json(paginatedFichas([FICHA_DISPONIVEL]));
      }),
    );

    await renderPage(BASE_ALUNO, []);

    fireEvent.click(screen.getAllByRole("button", { name: /vincular ficha/i })[0]);
    await waitFor(() => {
      expect(screen.getByRole("dialog")).toBeInTheDocument();
    });
    await waitFor(() => {
      expect(listFichasCalls).toBe(1);
    });

    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));
    await waitFor(() => {
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    });

    fireEvent.click(screen.getAllByRole("button", { name: /vincular ficha/i })[0]);
    await waitFor(() => {
      expect(screen.getByRole("dialog")).toBeInTheDocument();
    });
    expect(listFichasCalls).toBe(1);
  });

  it("selecionar ficha e confirmar chama vincularFichaAoAluno, mostra sucesso e recarrega a lista", async () => {
    let postedTreinoId: string | null = null;

    server.use(
      http.get("*/treinador/treinos", () => HttpResponse.json(paginatedFichas([FICHA_DISPONIVEL]))),
    );

    await renderPage(BASE_ALUNO, []);

    server.use(
      http.get("*/treinador/alunos/aluno-1/fichas", () => HttpResponse.json([FICHA_VINCULADA])),
      http.post("*/treinador/alunos/aluno-1/fichas/t-2", () => {
        postedTreinoId = "t-2";
        return HttpResponse.json({});
      }),
    );

    fireEvent.click(screen.getAllByRole("button", { name: /vincular ficha/i })[0]);
    await waitFor(() => {
      expect(screen.getByRole("dialog")).toBeInTheDocument();
    });

    const combobox = within(screen.getByRole("dialog")).getByRole("combobox");
    fireEvent.change(combobox, { target: { value: "Treino Full" } });

    const option = await screen.findByRole("option", { name: /Treino Full.*Hipertrofia/i });
    fireEvent.click(option);

    await waitFor(() => {
      expect(screen.getByRole("button", { name: "Vincular" })).toBeEnabled();
    });
    fireEvent.click(screen.getByRole("button", { name: "Vincular" }));

    await waitFor(() => {
      expect(postedTreinoId).toBe("t-2");
    });
    await waitFor(() => {
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    });

    expect(screen.getByText('Ficha "Treino Full" vinculada com sucesso.')).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.getByText("Fichas vinculadas (1)")).toBeInTheDocument();
    });
    expect(screen.getByText("Treino A")).toBeInTheDocument();
  });
});
