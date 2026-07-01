import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type { TreinoAlunoDetalheResponse } from "@/lib/api/aluno";

const mockPush = vi.fn();
const mockExportarFichaParaExcel = vi.fn().mockResolvedValue(undefined);

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: mockPush, back: vi.fn(), replace: vi.fn() })),
  useParams: vi.fn(() => ({ fichaId: "d1" })),
}));

vi.mock("@/lib/utils/excel", () => ({
  exportarFichaParaExcel: mockExportarFichaParaExcel,
}));

const FICHA_MULTI: TreinoAlunoDetalheResponse = {
  treinoAlunoId: "d1",
  treinoId: "t1",
  nomeTreino: "Treino Completo",
  objetivo: "Hipertrofia",
  status: "Ativo",
  exercicios: [
    {
      treinoExercicioId: "e1",
      exercicioId: "ex1",
      nomeExercicio: "Supino Reto",
      ordem: 1,
      observacao: "Manter cotovelos alinhados",
      series: [
        { serieConfigId: "s1", quantidade: 3, repeticoesMin: 8, repeticoesMax: 12, descricao: null, carga: 40, descanso: 60, ordem: 1 },
        { serieConfigId: "s2", quantidade: 2, repeticoesMin: 10, repeticoesMax: null, descricao: "até a falha", carga: null, descanso: null, ordem: 2 },
      ],
    },
    {
      treinoExercicioId: "e2",
      exercicioId: "ex2",
      nomeExercicio: "Agachamento",
      ordem: 2,
      observacao: null,
      series: [
        { serieConfigId: "s3", quantidade: 4, repeticoesMin: 10, repeticoesMax: null, descricao: null, carga: null, descanso: 90, ordem: 1 },
      ],
    },
  ],
};

const FICHA_SINGULAR: TreinoAlunoDetalheResponse = {
  treinoAlunoId: "d1",
  treinoId: "t2",
  nomeTreino: "Treino Simples",
  objetivo: "Forca",
  status: "Inativo",
  exercicios: [
    {
      treinoExercicioId: "e1",
      exercicioId: "ex1",
      nomeExercicio: "Rosca Direta",
      ordem: 1,
      observacao: null,
      series: [
        { serieConfigId: "s1", quantidade: 1, repeticoesMin: 10, repeticoesMax: null, descricao: null, carga: null, descanso: null, ordem: 1 },
      ],
    },
  ],
};

const FICHA_VAZIA: TreinoAlunoDetalheResponse = {
  treinoAlunoId: "d1",
  treinoId: "t3",
  nomeTreino: "Treino Vazio",
  objetivo: "Forca",
  status: "Ativo",
  exercicios: [],
};

async function renderDetalhe(ficha: TreinoAlunoDetalheResponse) {
  server.use(
    http.get("*/aluno/fichas/:id", () => HttpResponse.json(ficha)),
  );
  const { default: Page } = await import("@/app/(aluno)/aluno/fichas/[fichaId]/page");
  render(<Page />);
  await waitFor(() => {
    expect(screen.queryByRole("progressbar")).not.toBeInTheDocument();
  });
}

describe("DetalheFichaAlunoPage — ficha carregada", () => {
  afterEach(() => vi.clearAllMocks());

  it("exibe nome, objetivo, status e chips computados de exercícios/séries/descanso médio", async () => {
    await renderDetalhe(FICHA_MULTI);

    expect(screen.getByText("Treino Completo")).toBeInTheDocument();
    expect(screen.getByText("Hipertrofia")).toBeInTheDocument();
    expect(screen.getByLabelText("Status: Ativo")).toBeInTheDocument();
    expect(screen.getByText("2 exercícios")).toBeInTheDocument();
    expect(screen.getByText("9 séries no total")).toBeInTheDocument();
    expect(screen.getByText("~75s descanso médio")).toBeInTheDocument();
  });

  it("ficha com 1 exercício e 1 série usa singular e omite o chip de descanso médio", async () => {
    await renderDetalhe(FICHA_SINGULAR);

    expect(screen.getByText("1 exercício")).toBeInTheDocument();
    expect(screen.getByText("1 série no total")).toBeInTheDocument();
    expect(screen.queryByText(/descanso médio/)).not.toBeInTheDocument();
  });

  it("status Inativo não mostra o botão 'Iniciar treino'", async () => {
    await renderDetalhe(FICHA_SINGULAR);

    expect(screen.queryByRole("button", { name: /Iniciar treino/ })).not.toBeInTheDocument();
  });

  it("status Ativo mostra 'Iniciar treino' que navega para a tela de execução", async () => {
    await renderDetalhe(FICHA_MULTI);

    fireEvent.click(screen.getByRole("button", { name: /Iniciar treino/ }));

    expect(mockPush).toHaveBeenCalledWith("/aluno/fichas/d1/executar");
  });

  it("clicar em 'Exportar' chama exportarFichaParaExcel com nome, objetivo e exercícios da ficha", async () => {
    await renderDetalhe(FICHA_MULTI);

    fireEvent.click(screen.getByRole("button", { name: /Exportar/ }));

    expect(mockExportarFichaParaExcel).toHaveBeenCalledWith({
      nome: "Treino Completo",
      objetivo: "Hipertrofia",
      exercicios: FICHA_MULTI.exercicios,
    });
  });

  it("ficha sem exercícios exibe o estado vazio de exercícios", async () => {
    await renderDetalhe(FICHA_VAZIA);

    expect(screen.getByText("Nenhum exercício nesta ficha.")).toBeInTheDocument();
  });

  it("exibe intervalo de repetições, carga e descanso quando presentes, e omite quando nulos", async () => {
    await renderDetalhe(FICHA_MULTI);

    expect(screen.getByText("8–12 reps")).toBeInTheDocument();
    expect(screen.getAllByText("10 reps")).toHaveLength(2);
    expect(screen.getByText("até a falha")).toBeInTheDocument();
    expect(screen.getByText("40 kg")).toBeInTheDocument();
    expect(screen.getAllByText(/kg$/)).toHaveLength(1);
    expect(screen.getByText("60s")).toBeInTheDocument();
    expect(screen.getByText("90s")).toBeInTheDocument();
  });

  it("exibe a observação do exercício apenas quando presente", async () => {
    await renderDetalhe(FICHA_MULTI);

    expect(screen.getByText("Manter cotovelos alinhados")).toBeInTheDocument();
    expect(screen.getByText("Supino Reto")).toBeInTheDocument();
    expect(screen.getByText("Agachamento")).toBeInTheDocument();
  });
});
