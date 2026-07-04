import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor, fireEvent } from "@testing-library/react";
import { renderWithProviders } from "@/test/render";
import type { NotificacaoResponse } from "@/types";

vi.mock("@/lib/api/notificacoes", () => ({
  notificacoesApi: {
    contarNaoLidas: vi.fn(),
    listar: vi.fn(),
    marcarLida: vi.fn(),
  },
}));

import { notificacoesApi } from "@/lib/api/notificacoes";
import NotificacoesBell from "./NotificacoesBell";

const contarNaoLidas = vi.mocked(notificacoesApi.contarNaoLidas);
const listar = vi.mocked(notificacoesApi.listar);
const marcarLida = vi.mocked(notificacoesApi.marcarLida);

function notificacao(over: Partial<NotificacaoResponse> = {}): NotificacaoResponse {
  return {
    id: "n1",
    tipo: "NovoTreino",
    titulo: "Novo treino disponível",
    corpo: "Seu treinador liberou um treino.",
    linkRelativo: null,
    lida: false,
    createdAt: "2026-07-04T10:00:00Z",
    ...over,
  };
}

function ok<T>(data: T): never {
  return Promise.resolve({ data }) as never;
}

describe("NotificacoesBell", () => {
  beforeEach(() => {
    contarNaoLidas.mockReturnValue(ok({ total: 0 }));
    listar.mockReturnValue(ok<NotificacaoResponse[]>([]));
    marcarLida.mockResolvedValue({} as never);
  });

  it("exibe o contador de não-lidas do endpoint no badge", async () => {
    contarNaoLidas.mockReturnValue(ok({ total: 3 }));
    renderWithProviders(<NotificacoesBell />, { skipAuth: true });
    expect(
      await screen.findByRole("button", { name: "Notificações, 3 não lidas" }),
    ).toBeInTheDocument();
    expect(screen.getByText("3")).toBeInTheDocument();
  });

  it("sem não-lidas o badge não anuncia contagem", async () => {
    renderWithProviders(<NotificacoesBell />, { skipAuth: true });
    await waitFor(() => expect(contarNaoLidas).toHaveBeenCalled());
    expect(screen.getByRole("button", { name: "Notificações" })).toBeInTheDocument();
  });

  it("abrir com itens lista título e corpo", async () => {
    listar.mockReturnValue(ok([notificacao({ titulo: "Parabéns!", corpo: "Você treinou hoje." })]));
    renderWithProviders(<NotificacoesBell />, { skipAuth: true });
    fireEvent.click(await screen.findByRole("button", { name: /Notificações/ }));
    expect(await screen.findByText("Parabéns!")).toBeInTheDocument();
    expect(screen.getByText("Você treinou hoje.")).toBeInTheDocument();
  });

  it("abrir sem notificações mostra estado vazio", async () => {
    renderWithProviders(<NotificacoesBell />, { skipAuth: true });
    fireEvent.click(await screen.findByRole("button", { name: /Notificações/ }));
    expect(await screen.findByText("Nenhuma notificação por aqui.")).toBeInTheDocument();
  });

  it("marcar-lida chama o endpoint e derruba o badge", async () => {
    contarNaoLidas.mockReturnValue(ok({ total: 2 }));
    listar.mockReturnValue(ok([notificacao({ id: "abc", titulo: "Novo treino", lida: false })]));
    renderWithProviders(<NotificacoesBell />, { skipAuth: true });
    fireEvent.click(await screen.findByRole("button", { name: "Notificações, 2 não lidas" }));
    fireEvent.click(await screen.findByText("Novo treino"));
    await waitFor(() => expect(marcarLida).toHaveBeenCalledWith("abc"));
    expect(
      await screen.findByRole("button", { name: "Notificações, 1 não lidas", hidden: true }),
    ).toBeInTheDocument();
  });

  it("erro ao carregar a lista exibe alerta", async () => {
    listar.mockRejectedValue({ response: { data: { detail: "Falha ao listar." } } });
    renderWithProviders(<NotificacoesBell />, { skipAuth: true });
    fireEvent.click(await screen.findByRole("button", { name: /Notificações/ }));
    expect(await screen.findByText("Falha ao listar.")).toBeInTheDocument();
  });
});
