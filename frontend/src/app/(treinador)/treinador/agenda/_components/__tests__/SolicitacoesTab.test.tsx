import { describe, it, expect } from "vitest";
import { screen, waitFor, fireEvent } from "@testing-library/react";
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

    const inicio = new Date(SOLICITACAO_BASE.inicioUtc);
    const fim = new Date(SOLICITACAO_BASE.fimUtc);
    const intervaloEsperado = `${inicio.toLocaleDateString("pt-BR")} ${inicio.toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" })} — ${fim.toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" })}`;
    expect(screen.getByText(intervaloEsperado)).toBeInTheDocument();
  });

  it("carrega pendentes e confirmadas juntas na primeira busca", async () => {
    server.use(
      http.get("*/treinador/agenda/solicitacoes", () =>
        HttpResponse.json({
          items: [
            SOLICITACAO_BASE,
            {
              ...SOLICITACAO_BASE, id: "s2", pacoteNome: "Personal",
              status: "Confirmada" as const, leadId: "l2", leadNome: "João Souza", leadContatoValor: "joao@exemplo.com",
            },
          ],
          total: 2, pagina: 1, tamanhoPagina: 20,
        })),
    );
    renderTab();

    expect(await screen.findByText("Aula experimental")).toBeInTheDocument();
    expect(screen.getByText("Personal")).toBeInTheDocument();
    expect(screen.getByText("Maria Silva")).toBeInTheDocument();
    expect(screen.getByText("João Souza")).toBeInTheDocument();
    expect(screen.getByText("Pendente")).toBeInTheDocument();
    expect(screen.getByText("Confirmada")).toBeInTheDocument();
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

  it("confirma uma solicitação pendente e reflete o novo status após a resposta", async () => {
    let confirmada = false;
    server.use(
      http.get("*/treinador/agenda/solicitacoes", () =>
        HttpResponse.json({
          items: [{ ...SOLICITACAO_BASE, status: confirmada ? "Confirmada" : "PendenteAgente" }],
          total: 1, pagina: 1, tamanhoPagina: 20,
        })),
      http.post("*/treinador/agenda/solicitacoes/s1/confirmar", () => {
        confirmada = true;
        return HttpResponse.json({});
      }),
    );
    renderTab();

    await screen.findByText("Pendente");
    fireEvent.click(screen.getByRole("button", { name: "Confirmar" }));

    expect(await screen.findByText("Confirmada")).toBeInTheDocument();
    expect(screen.queryByText("Pendente")).not.toBeInTheDocument();
  });

  it("recusa uma solicitação pendente com motivo opcional", async () => {
    let corpoEnviado: Record<string, unknown> | null = null;
    server.use(
      http.get("*/treinador/agenda/solicitacoes", () =>
        HttpResponse.json({ items: [SOLICITACAO_BASE], total: 1, pagina: 1, tamanhoPagina: 20 })),
      http.post("*/treinador/agenda/solicitacoes/s1/recusar", async ({ request }) => {
        corpoEnviado = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({});
      }),
    );
    renderTab();

    fireEvent.click(await screen.findByRole("button", { name: "Recusar" }));
    await screen.findByRole("dialog");
    fireEvent.change(screen.getByLabelText(/motivo/i), { target: { value: "Fora do horário disponível" } });
    fireEvent.click(screen.getAllByRole("button", { name: "Recusar" }).at(-1)!);

    await waitFor(() => expect(corpoEnviado).toEqual({ motivo: "Fora do horário disponível" }));
  });

  it("cancela uma solicitação confirmada e devolve a capacidade", async () => {
    server.use(
      http.get("*/treinador/agenda/solicitacoes", () =>
        HttpResponse.json({
          items: [{ ...SOLICITACAO_BASE, status: "Confirmada" }],
          total: 1, pagina: 1, tamanhoPagina: 20,
        })),
      http.post("*/treinador/agenda/solicitacoes/s1/cancelar", () => HttpResponse.json({})),
    );
    renderTab();

    fireEvent.click(await screen.findByRole("button", { name: "Cancelar" }));
    await screen.findByRole("dialog");
    fireEvent.click(screen.getAllByRole("button", { name: /^cancelar agendamento$/i })[0]);

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
  });

  it("erro genérico ao confirmar exibe mensagem e mantém o status exibido", async () => {
    server.use(
      http.get("*/treinador/agenda/solicitacoes", () =>
        HttpResponse.json({ items: [SOLICITACAO_BASE], total: 1, pagina: 1, tamanhoPagina: 20 })),
      http.post("*/treinador/agenda/solicitacoes/s1/confirmar", () =>
        HttpResponse.json({ detail: "Solicitação não encontrada." }, { status: 404 })),
    );
    renderTab();

    fireEvent.click(await screen.findByRole("button", { name: "Confirmar" }));

    expect(await screen.findByText(/solicitação não encontrada/i)).toBeInTheDocument();
    expect(screen.getByText("Pendente")).toBeInTheDocument();
  });

  it("conflito de capacidade (409) ao confirmar mostra mensagem específica", async () => {
    server.use(
      http.get("*/treinador/agenda/solicitacoes", () =>
        HttpResponse.json({ items: [SOLICITACAO_BASE], total: 1, pagina: 1, tamanhoPagina: 20 })),
      http.post("*/treinador/agenda/solicitacoes/s1/confirmar", () =>
        HttpResponse.json({ detail: "solicitacao_agendamento.capacidade_esgotada" }, { status: 409 })),
    );
    renderTab();

    fireEvent.click(await screen.findByRole("button", { name: "Confirmar" }));

    expect(await screen.findByText(/capacidade máxima deste horário já foi atingida/i)).toBeInTheDocument();
  });
});
