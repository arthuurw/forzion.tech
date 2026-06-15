/**
 * Ações da página /admin/treinadores: aprovar, reprovar, inativar, excluir,
 * atribuir plano, filtro de status e navegação para detalhe.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, fireEvent, waitFor, within } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type { TreinadorResponse } from "@/types";

const mockPush = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: mockPush, back: vi.fn(), replace: vi.fn() }),
  useParams: () => ({}),
}));

import TreinadoresAdminPage from "../page";
import { renderWithProviders } from "@/test/render";

function treinador(over: Partial<TreinadorResponse>): TreinadorResponse {
  return {
    treinadorId: "t1",
    nome: "Coach Um",
    email: "coach@x.com",
    status: "AguardandoAprovacao",
    createdAt: "2026-01-01T00:00:00Z",
    planoPlataformaId: null,
    ...over,
  } as TreinadorResponse;
}

const LISTA = [
  treinador({ treinadorId: "t-aguard", nome: "Aguardando Coach", status: "AguardandoAprovacao" }),
  treinador({ treinadorId: "t-ativo", nome: "Ativo Coach", status: "Ativo" }),
  treinador({ treinadorId: "t-inativo", nome: "Inativo Coach", status: "Inativo" }),
];

beforeEach(() => {
  mockPush.mockClear();
  server.use(
    http.get("*/api/auth/me", () =>
      HttpResponse.json({ contaId: "c1", tipoConta: "SystemAdmin", perfilId: "p1" }),
    ),
    http.get("*/admin/treinadores", () =>
      HttpResponse.json({ items: LISTA, total: LISTA.length, pagina: 1, tamanhoPagina: 10 }),
    ),
    http.get("*/admin/planos", () =>
      HttpResponse.json([
        { planoId: "pl1", nome: "Basic", tier: "Basic", maxAlunos: 10, preco: 49.9, descricao: null, isAtivo: true },
      ]),
    ),
  );
});

async function waitList() {
  expect(await screen.findByText("Aguardando Coach")).toBeInTheDocument();
}

describe("/admin/treinadores — ações", () => {
  it("aprovar treinador chama POST e mostra sucesso", async () => {
    let called = false;
    server.use(
      http.post("*/admin/treinadores/t-aguard/aprovar", () => {
        called = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderWithProviders(<TreinadoresAdminPage />);
    await waitList();

    fireEvent.click(screen.getByLabelText("Aprovar treinador"));
    const dialog = await screen.findByRole("dialog");
    fireEvent.change(within(dialog).getByLabelText(/observação/i), { target: { value: "ok" } });
    fireEvent.click(within(dialog).getByRole("button", { name: /^aprovar$/i }));

    await waitFor(() => expect(called).toBe(true));
  });

  it("reprovar treinador chama POST", async () => {
    let called = false;
    server.use(
      http.post("*/admin/treinadores/t-aguard/reprovar", () => {
        called = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderWithProviders(<TreinadoresAdminPage />);
    await waitList();

    fireEvent.click(screen.getByLabelText("Reprovar treinador"));
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: /^reprovar$/i }));

    await waitFor(() => expect(called).toBe(true));
  });

  it("inativar treinador chama POST", async () => {
    let called = false;
    server.use(
      http.post("*/admin/treinadores/t-ativo/inativar", () => {
        called = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderWithProviders(<TreinadoresAdminPage />);
    await waitList();

    fireEvent.click(screen.getByLabelText("Inativar treinador"));
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: /^desativar$/i }));

    await waitFor(() => expect(called).toBe(true));
  });

  it("excluir treinador chama DELETE", async () => {
    let called = false;
    server.use(
      http.delete("*/admin/treinadores/t-inativo", () => {
        called = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderWithProviders(<TreinadoresAdminPage />);
    await waitList();

    fireEvent.click(screen.getByLabelText("Excluir treinador permanentemente"));
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: /^excluir$/i }));

    await waitFor(() => expect(called).toBe(true));
  });

  it("erro ao aprovar exibe banner", async () => {
    server.use(http.post("*/admin/treinadores/t-aguard/aprovar", () => new HttpResponse(null, { status: 500 })));
    renderWithProviders(<TreinadoresAdminPage />);
    await waitList();

    fireEvent.click(screen.getByLabelText("Aprovar treinador"));
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: /^aprovar$/i }));

    expect(await screen.findByText(/erro ao aprovar treinador/i)).toBeInTheDocument();
  });

  it("atribuir plano: abre dialog, seleciona e confirma", async () => {
    let body: unknown = null;
    server.use(
      http.patch("*/admin/treinadores/t-ativo/plano", async ({ request }) => {
        body = await request.json();
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderWithProviders(<TreinadoresAdminPage />);
    await waitList();

    const planoButtons = screen.getAllByLabelText("Atribuir plano");
    fireEvent.click(planoButtons[1]); // treinador Ativo
    const dialog = await screen.findByRole("dialog");

    const input = within(dialog).getByLabelText(/novo plano/i);
    fireEvent.mouseDown(input);
    fireEvent.change(input, { target: { value: "Basic" } });
    fireEvent.click(await screen.findByText(/Basic \(até 10 alunos\)/i));

    fireEvent.click(within(dialog).getByRole("button", { name: /confirmar/i }));
    await waitFor(() => expect(body).toEqual({ planoId: "pl1" }));
  });

  it("filtro de status dispara nova busca", async () => {
    const statuses: string[] = [];
    server.use(
      http.get("*/admin/treinadores", ({ request }) => {
        const s = new URL(request.url).searchParams.get("status");
        if (s) statuses.push(s);
        return HttpResponse.json({ items: LISTA, total: LISTA.length, pagina: 1, tamanhoPagina: 10 });
      }),
    );
    renderWithProviders(<TreinadoresAdminPage />);
    await waitList();

    fireEvent.mouseDown(screen.getAllByRole("combobox")[0]);
    fireEvent.click(await screen.findByRole("option", { name: "Ativo" }));
    await waitFor(() => expect(statuses).toContain("Ativo"));
  });

  it("ver detalhe navega para a página do treinador", async () => {
    renderWithProviders(<TreinadoresAdminPage />);
    await waitList();

    fireEvent.click(screen.getAllByLabelText("Ver detalhe do treinador")[0]);
    expect(mockPush).toHaveBeenCalledWith("/admin/treinadores/t-aguard");
  });

  // R5 — em mobile (<md) as ações colapsam num kebab Menu; itens alcançáveis e
  // disparam o handler certo.
  describe("mobile (<md) → kebab de ações", () => {
    let original: typeof window.matchMedia;
    beforeEach(() => {
      original = window.matchMedia;
      window.matchMedia = vi.fn().mockImplementation((query: string) => ({
        matches: true,
        media: query,
        onchange: null,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        addListener: vi.fn(),
        removeListener: vi.fn(),
        dispatchEvent: vi.fn(),
      }));
    });
    afterEach(() => {
      window.matchMedia = original;
    });

    it("abre kebab do treinador Aguardando e expõe Aprovar/Reprovar/Atribuir plano/Ver detalhe", async () => {
      renderWithProviders(<TreinadoresAdminPage />);
      await waitList();

      fireEvent.click(screen.getByLabelText("Ações de Aguardando Coach"));
      const menu = await screen.findByRole("menu");
      expect(within(menu).getByText("Aprovar")).toBeInTheDocument();
      expect(within(menu).getByText("Reprovar")).toBeInTheDocument();
      expect(within(menu).getByText("Atribuir plano")).toBeInTheDocument();
      expect(within(menu).getByText("Ver detalhe")).toBeInTheDocument();
    });

    it("item 'Ver detalhe' do kebab navega para a página do treinador", async () => {
      renderWithProviders(<TreinadoresAdminPage />);
      await waitList();

      fireEvent.click(screen.getByLabelText("Ações de Aguardando Coach"));
      const menu = await screen.findByRole("menu");
      fireEvent.click(within(menu).getByText("Ver detalhe"));
      expect(mockPush).toHaveBeenCalledWith("/admin/treinadores/t-aguard");
    });
  });
});
