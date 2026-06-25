import React from "react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor, act, within } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type { TreinoResponse, ExercicioResponse } from "@/types";

const mockPush = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: mockPush, back: vi.fn(), replace: vi.fn() })),
  useParams: vi.fn(() => ({ treinoId: "treino-1" })),
}));

const FICHA: TreinoResponse = {
  treinoId: "treino-1",
  nome: "Treino A",
  objetivo: "Hipertrofia",
  dificuldade: "Iniciante",
  dataInicio: null,
  dataFim: null,
  treinadorId: "t-1",
  exercicios: [],
  createdAt: "2024-01-01T00:00:00Z",
  updatedAt: null,
};

const EX_SUPINO: ExercicioResponse = {
  exercicioId: "ex-1",
  nome: "Supino",
  descricao: null,
  grupoMuscularId: "gm-1",
  grupoMuscular: "Peito",
  treinadorId: "t-1",
  isGlobal: false,
};

const EX_AGACH: ExercicioResponse = {
  exercicioId: "ex-2",
  nome: "Agachamento",
  descricao: null,
  grupoMuscularId: "gm-2",
  grupoMuscular: "Pernas",
  treinadorId: "t-1",
  isGlobal: false,
};

function paginated(items: ExercicioResponse[]) {
  return { items, total: items.length, pagina: 1, tamanhoPagina: 20 };
}

async function renderPage() {
  server.use(
    http.get("*/treinos/treino-1", () => HttpResponse.json(FICHA)),
  );
  const { default: Page } = await import(
    "@/app/(treinador)/treinador/treinos/[treinoId]/page"
  );
  render(<Page />);
  await waitFor(() => {
    expect(screen.queryByRole("progressbar")).not.toBeInTheDocument();
  });
}

async function openAddDialog() {
  fireEvent.click(screen.getAllByRole("button", { name: /adicionar exercício/i })[0]);
  await waitFor(() => {
    expect(screen.getByRole("dialog")).toBeInTheDocument();
  });
  return within(screen.getByRole("dialog")).getByRole("combobox");
}

describe("DetalheFichaPage — Autocomplete server-side search (T3)", () => {
  beforeEach(() => {
    mockPush.mockClear();
  });

  it("digitar no Autocomplete dispara busca server-side e reflete subset filtrado", async () => {
    await renderPage();
    server.use(
      http.get("*/treinador/exercicios", ({ request }) => {
        const url = new URL(request.url);
        const nome = url.searchParams.get("nome") ?? "";
        const items = [EX_SUPINO, EX_AGACH].filter((e) =>
          e.nome.toLowerCase().includes(nome.toLowerCase()),
        );
        return HttpResponse.json(paginated(items));
      }),
    );

    const input = await openAddDialog();
    fireEvent.change(input, { target: { value: "supino" } });

    await waitFor(() => {
      expect(screen.getByRole("option", { name: /Supino/i })).toBeInTheDocument();
    }, { timeout: 600 });
    expect(screen.queryByRole("option", { name: /Agachamento/i })).not.toBeInTheDocument();
  });

  it("selecionar opção e adicionar à ficha preserva selectedEx e dispara POST correto", async () => {
    await renderPage();
    let postedBody: Record<string, unknown> | null = null;
    server.use(
      http.get("*/treinador/exercicios", () => HttpResponse.json(paginated([EX_SUPINO]))),
      http.post("*/treinos/treino-1/exercicios", async ({ request }) => {
        postedBody = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({});
      }),
      http.get("*/treinos/treino-1", () => HttpResponse.json(FICHA)),
    );

    const input = await openAddDialog();
    fireEvent.change(input, { target: { value: "sup" } });

    const option = await screen.findByRole("option", { name: /Supino/i });
    fireEvent.click(option);

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /^Adicionar$/i })).toBeEnabled();
    });

    fireEvent.click(screen.getByRole("button", { name: /^Adicionar$/i }));

    await waitFor(() => {
      expect(postedBody).not.toBeNull();
    });
    expect((postedBody as unknown as Record<string, unknown>).exercicioId).toBe("ex-1");
  });

  it("digitação rápida não dispara request por tecla (debounce 300ms)", async () => {
    await renderPage();

    let callCount = 0;
    server.use(
      http.get("*/treinador/exercicios", () => {
        callCount++;
        return HttpResponse.json(paginated([]));
      }),
    );

    const input = await openAddDialog();

    vi.useFakeTimers();
    try {
      fireEvent.change(input, { target: { value: "a" } });
      fireEvent.change(input, { target: { value: "ag" } });
      fireEvent.change(input, { target: { value: "aga" } });

      expect(callCount).toBe(0);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(350);
      });

      expect(callCount).toBe(1);
    } finally {
      vi.runOnlyPendingTimers();
      vi.useRealTimers();
    }
  });
});
