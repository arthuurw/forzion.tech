import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type { LeadListItem, ListarLeadsResponse } from "@/types";

const push = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
}));

function lead(overrides: Partial<LeadListItem> = {}): LeadListItem {
  return {
    id: "lead-1",
    nome: "Maria Silva",
    contatoTipo: "Email",
    contatoValor: "maria@example.com",
    source: "Agent",
    status: "Novo",
    createdAt: "2026-08-20T10:00:00Z",
    anonimizado: false,
    ...overrides,
  };
}

function listResponse(items: LeadListItem[]): ListarLeadsResponse {
  return { items, total: items.length, pagina: 1, tamanhoPagina: 20 };
}

async function renderPage() {
  const { default: Page } = await import("../page");
  render(<Page />);
}

describe("LeadsTreinadorPage", () => {
  beforeEach(() => {
    push.mockClear();
  });

  it("lista leads do treinador com origem, status e chegada", async () => {
    server.use(http.get("*/treinador/leads", () => HttpResponse.json(listResponse([lead()]))));
    await renderPage();

    expect(await screen.findByText("Maria Silva")).toBeInTheDocument();
    expect(screen.getByText(/maria@example\.com/)).toBeInTheDocument();
    expect(screen.getByText("Agente")).toBeInTheDocument();
    expect(screen.getByText("Novo")).toBeInTheDocument();
  });

  it("estado vazio explica os dois canais de entrada", async () => {
    server.use(http.get("*/treinador/leads", () => HttpResponse.json(listResponse([]))));
    await renderPage();

    expect(await screen.findByText(/nenhum lead ainda/i)).toBeInTheDocument();
    expect(screen.getByText(/cadastrar um lead manualmente/i)).toBeInTheDocument();
  });

  it("estado de erro exibe alerta", async () => {
    server.use(http.get("*/treinador/leads", () => HttpResponse.json({ title: "Erro" }, { status: 500 })));
    await renderPage();

    expect(await screen.findByText("Erro ao carregar leads.")).toBeInTheDocument();
  });

  it("filtro de status envia o parâmetro correto", async () => {
    let recebidoStatus: string | null = null;
    server.use(
      http.get("*/treinador/leads", ({ request }) => {
        recebidoStatus = new URL(request.url).searchParams.get("status");
        return HttpResponse.json(listResponse([]));
      }),
    );
    await renderPage();
    await screen.findByText(/nenhum lead ainda/i);

    fireEvent.mouseDown(screen.getByLabelText("Status"));
    fireEvent.click(await screen.findByRole("option", { name: "Em contato" }));

    await waitFor(() => expect(recebidoStatus).toBe("EmContato"));
  });

  it("filtro de origem envia o parâmetro correto", async () => {
    let recebidoOrigem: string | null = null;
    server.use(
      http.get("*/treinador/leads", ({ request }) => {
        recebidoOrigem = new URL(request.url).searchParams.get("origem");
        return HttpResponse.json(listResponse([]));
      }),
    );
    await renderPage();
    await screen.findByText(/nenhum lead ainda/i);

    fireEvent.mouseDown(screen.getByLabelText("Origem"));
    fireEvent.click(await screen.findByRole("option", { name: "Manual" }));

    await waitFor(() => expect(recebidoOrigem).toBe("Manual"));
  });

  it("busca por termo dispara ao pressionar Enter", async () => {
    let recebidoTermo: string | null = null;
    server.use(
      http.get("*/treinador/leads", ({ request }) => {
        recebidoTermo = new URL(request.url).searchParams.get("termo");
        return HttpResponse.json(listResponse([]));
      }),
    );
    await renderPage();
    await screen.findByText(/nenhum lead ainda/i);

    fireEvent.change(screen.getByLabelText(/buscar por nome ou contato/i), { target: { value: "maria" } });
    fireEvent.keyDown(screen.getByLabelText(/buscar por nome ou contato/i), { key: "Enter" });

    await waitFor(() => expect(recebidoTermo).toBe("maria"));
  });

  it("período (de/até) é enviado na consulta", async () => {
    let recebidoInicio: string | null = null;
    let recebidoFim: string | null = null;
    server.use(
      http.get("*/treinador/leads", ({ request }) => {
        const url = new URL(request.url);
        recebidoInicio = url.searchParams.get("inicio");
        recebidoFim = url.searchParams.get("fim");
        return HttpResponse.json(listResponse([]));
      }),
    );
    await renderPage();
    await screen.findByText(/nenhum lead ainda/i);

    fireEvent.change(screen.getByLabelText("De"), { target: { value: "2026-07-01" } });
    fireEvent.change(screen.getByLabelText("Até"), { target: { value: "2026-08-01" } });

    await waitFor(() => expect(recebidoInicio).toBe("2026-07-01"));
    await waitFor(() => expect(recebidoFim).toBe("2026-08-01"));
  });

  it("clicar num lead navega para o detalhe", async () => {
    server.use(http.get("*/treinador/leads", () => HttpResponse.json(listResponse([lead()]))));
    await renderPage();

    fireEvent.click(await screen.findByText("Maria Silva"));
    expect(push).toHaveBeenCalledWith("/treinador/leads/lead-1");
  });
});
