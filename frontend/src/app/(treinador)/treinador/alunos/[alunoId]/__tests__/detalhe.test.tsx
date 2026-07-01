import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type { AlunoResponse } from "@/types";

const mockPush = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: mockPush, back: vi.fn(), replace: vi.fn() })),
  useParams: vi.fn(() => ({ alunoId: "aluno-1" })),
}));

const BASE_ALUNO: AlunoResponse = {
  alunoId: "aluno-1",
  nome: "João",
  email: null,
  telefone: null,
  status: "AguardandoAprovacao",
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

async function renderPage(aluno: AlunoResponse, onForbidden?: () => void) {
  server.use(
    http.get("*/treinador/alunos/aluno-1/fichas", () => HttpResponse.json([])),
    http.get("*/treinador/alunos/aluno-1", () => HttpResponse.json(aluno)),
    http.get("*/treinador/vinculos", () => {
      onForbidden?.();
      return HttpResponse.json({ items: [], total: 0, pagina: 1, tamanhoPagina: 20 });
    }),
    http.get("*/treinador/pacotes", () => {
      onForbidden?.();
      return HttpResponse.json([]);
    }),
  );
  const { default: Page } = await import(
    "@/app/(treinador)/treinador/alunos/[alunoId]/page"
  );
  render(<Page />);
  await waitFor(() => {
    expect(screen.queryByRole("progressbar")).not.toBeInTheDocument();
  });
}

describe("DetalheAlunoPage — pacote via getAluno (T2)", () => {
  beforeEach(() => {
    mockPush.mockClear();
  });

  it("renderiza o pacote vindo de getAluno sem buscar vinculos/pacotes", async () => {
    const forbidden = vi.fn();
    await renderPage(
      { ...BASE_ALUNO, pacoteId: "p-1", pacoteNome: "Plano Premium" },
      forbidden,
    );

    expect(screen.getByText("Plano Premium")).toBeInTheDocument();
    expect(forbidden).not.toHaveBeenCalled();
  });

  it("sem pacote oculta a seção de pacote", async () => {
    await renderPage(BASE_ALUNO);

    expect(screen.queryByText(/Pacote:/i)).not.toBeInTheDocument();
  });

  it("erro ao carregar aluno exibe alerta de erro", async () => {
    server.use(
      http.get("*/treinador/alunos/aluno-1/fichas", () => HttpResponse.json([])),
      http.get("*/treinador/alunos/aluno-1", () => HttpResponse.json({}, { status: 500 })),
    );
    const { default: Page } = await import(
      "@/app/(treinador)/treinador/alunos/[alunoId]/page"
    );
    render(<Page />);

    expect(
      await screen.findByText("Erro ao carregar dados do aluno."),
    ).toBeInTheDocument();
  });
});
