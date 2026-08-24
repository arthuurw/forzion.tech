import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { renderWithProviders } from "@/test/render";
import SolicitacoesTab from "../SolicitacoesTab";

const SOLICITACAO_BASE = {
  id: "s1", pacoteId: "p1", pacoteNome: "Aula experimental",
  inicioUtc: "2026-09-01T13:00:00.000Z", fimUtc: "2026-09-01T14:00:00.000Z",
  status: "PendenteAgente" as const, motivo: null, createdAt: "2026-08-20T00:00:00.000Z",
  leadId: "l1", leadNome: "Maria Silva", leadContatoTipo: "Email" as const, leadContatoValor: "maria@exemplo.com",
  leadAnonimizado: false,
};

function renderTab() {
  return renderWithProviders(<SolicitacoesTab />, { skipAuth: true });
}

describe("SolicitacoesTab", () => {
  it("lista solicitações com serviço, status e dados do lead", async () => {
    server.use(
      http.get("*/treinador/agenda/solicitacoes", () =>
        HttpResponse.json({ items: [SOLICITACAO_BASE], total: 1, pagina: 1, tamanhoPagina: 20 })),
    );
    renderTab();

    expect(await screen.findByText("Aula experimental")).toBeInTheDocument();
    expect(screen.getByText("Maria Silva")).toBeInTheDocument();
    expect(screen.getByText(/e-mail: maria@exemplo.com/i)).toBeInTheDocument();
    expect(screen.getByText("Pendente")).toBeInTheDocument();
  });

  it("exibe estado vazio quando não há solicitações", async () => {
    server.use(
      http.get("*/treinador/agenda/solicitacoes", () => HttpResponse.json({ items: [], total: 0, pagina: 1, tamanhoPagina: 20 })),
    );
    renderTab();

    expect(await screen.findByText(/nenhuma solicitação de agendamento ainda/i)).toBeInTheDocument();
  });

  it("esconde o contato quando o lead está anonimizado", async () => {
    server.use(
      http.get("*/treinador/agenda/solicitacoes", () =>
        HttpResponse.json({
          items: [{ ...SOLICITACAO_BASE, leadAnonimizado: true, leadNome: "", leadContatoValor: "" }],
          total: 1, pagina: 1, tamanhoPagina: 20,
        })),
    );
    renderTab();

    expect(await screen.findByText(/lead anonimizado/i)).toBeInTheDocument();
    expect(screen.queryByText("maria@exemplo.com")).not.toBeInTheDocument();
  });

  it("exibe erro quando o carregamento falha", async () => {
    server.use(
      http.get("*/treinador/agenda/solicitacoes", () => HttpResponse.json({ detail: "Falha ao listar." }, { status: 500 })),
    );
    renderTab();

    expect(await screen.findByText(/falha ao listar/i)).toBeInTheDocument();
  });
});
