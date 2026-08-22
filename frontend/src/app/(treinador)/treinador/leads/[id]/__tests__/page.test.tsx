import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type { LeadDetalheResponse } from "@/types";

vi.mock("next/navigation", () => ({
  useParams: () => ({ id: "lead-1" }),
}));

function leadDetalhe(overrides: Partial<LeadDetalheResponse> = {}): LeadDetalheResponse {
  return {
    id: "lead-1",
    nome: "Maria Silva",
    contatoTipo: "Email",
    contatoValor: "maria@example.com",
    interesse: "Musculação e emagrecimento",
    consentimentoFinalidade: "Contato comercial sobre planos de treino",
    consentimentoConcedidoEm: "2026-08-20T10:00:00Z",
    consentimentoRegistradoEm: "2026-08-20T10:00:01Z",
    origemUserAgent: null,
    origemAssistente: null,
    source: "Agent",
    status: "Novo",
    motivoDescarte: null,
    alunoId: null,
    anonimizado: false,
    ultimoToqueEm: "2026-08-20T10:00:00Z",
    createdAt: "2026-08-20T10:00:00Z",
    updatedAt: null,
    historico: [],
    ...overrides,
  };
}

async function renderPage() {
  const { default: Page } = await import("../page");
  render(<Page />);
}

describe("DetalheLeadPage", () => {
  beforeEach(() => {
    server.use(http.get("*/treinador/leads/lead-1", () => HttpResponse.json(leadDetalhe())));
  });

  it("exibe contato, interesse, origem e consentimento", async () => {
    await renderPage();

    expect(await screen.findByRole("heading", { name: "Maria Silva" })).toBeInTheDocument();
    expect(screen.getByText(/maria@example\.com/)).toBeInTheDocument();
    expect(screen.getByText(/musculação e emagrecimento/i)).toBeInTheDocument();
    expect(screen.getByText("Agente")).toBeInTheDocument();
    expect(screen.getByText(/contato comercial sobre planos de treino/i)).toBeInTheDocument();
  });

  it("exibe o histórico em ordem cronológica", async () => {
    server.use(http.get("*/treinador/leads/lead-1", () => HttpResponse.json(leadDetalhe({
      historico: [
        { id: "h1", ocorridaEm: "2026-08-20T10:00:00Z", statusAnterior: null, statusNovo: null, observacao: "Primeira nota", realizadoPorId: null },
        { id: "h2", ocorridaEm: "2026-08-21T10:00:00Z", statusAnterior: "Novo", statusNovo: "EmContato", observacao: null, realizadoPorId: "t-1" },
      ],
    }))));
    await renderPage();

    expect(await screen.findByText("Primeira nota")).toBeInTheDocument();
    expect(screen.getByText(/status alterado para "em contato"/i)).toBeInTheDocument();
  });

  it("marcar em contato chama o endpoint e recarrega", async () => {
    let corpo: unknown = null;
    server.use(
      http.patch("*/treinador/leads/lead-1/status", async ({ request }) => {
        corpo = await request.json();
        return HttpResponse.json({ leadId: "lead-1", status: "EmContato" });
      }),
    );
    await renderPage();

    fireEvent.click(await screen.findByRole("button", { name: "Marcar em contato" }));

    await waitFor(() => expect(corpo).toEqual({ novoStatus: "EmContato" }));
    expect(await screen.findByText("Lead marcado como em contato.")).toBeInTheDocument();
  });

  it("registrar interação chama o endpoint com a observação", async () => {
    let corpo: unknown = null;
    server.use(
      http.post("*/treinador/leads/lead-1/interacoes", async ({ request }) => {
        corpo = await request.json();
        return HttpResponse.json({ leadId: "lead-1" });
      }),
    );
    await renderPage();

    fireEvent.change(await screen.findByLabelText(/registrar interação/i), { target: { value: "Liguei, sem resposta." } });
    fireEvent.click(screen.getByRole("button", { name: "Registrar" }));

    await waitFor(() => expect(corpo).toEqual({ observacao: "Liguei, sem resposta." }));
  });

  it("descartar sem selecionar motivo mostra erro e não chama a API", async () => {
    const patch = vi.fn();
    server.use(http.patch("*/treinador/leads/lead-1/status", () => { patch(); return HttpResponse.json({}); }));
    await renderPage();

    fireEvent.click(await screen.findByRole("button", { name: "Descartar" }));
    await screen.findByRole("dialog");
    fireEvent.click(screen.getAllByRole("button", { name: "Descartar" }).at(-1)!);

    expect(await screen.findByText(/selecione um motivo/i)).toBeInTheDocument();
    expect(patch).not.toHaveBeenCalled();
  });

  it("descartar com motivo válido chama o endpoint com o motivo escolhido", async () => {
    let corpo: unknown = null;
    server.use(
      http.patch("*/treinador/leads/lead-1/status", async ({ request }) => {
        corpo = await request.json();
        return HttpResponse.json({ leadId: "lead-1", status: "Descartado" });
      }),
    );
    await renderPage();

    fireEvent.click(await screen.findByRole("button", { name: "Descartar" }));
    await screen.findByRole("dialog");
    fireEvent.mouseDown(screen.getByRole("combobox"));
    fireEvent.click(await screen.findByRole("option", { name: "Sem interesse" }));
    fireEvent.click(screen.getAllByRole("button", { name: "Descartar" }).at(-1)!);

    await waitFor(() => expect(corpo).toEqual({ novoStatus: "Descartado", motivo: "SemInteresse", observacao: null }));
  });

  it("lead em estado terminal não exibe ações e mostra aviso", async () => {
    server.use(http.get("*/treinador/leads/lead-1", () => HttpResponse.json(leadDetalhe({ status: "Convertido" }))));
    await renderPage();

    await screen.findByRole("heading", { name: "Maria Silva" });
    expect(screen.getByText(/já foi finalizado/i)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Marcar em contato" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Descartar" })).not.toBeInTheDocument();
  });

  it("lead anonimizado não exibe ações e mostra aviso específico", async () => {
    server.use(http.get("*/treinador/leads/lead-1", () => HttpResponse.json(leadDetalhe({ anonimizado: true }))));
    await renderPage();

    await screen.findByRole("heading", { name: "Maria Silva" });
    expect(screen.getByText(/anonimizado por política de retenção/i)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Marcar em contato" })).not.toBeInTheDocument();
  });

  it("erro ao carregar exibe tela de erro com retry", async () => {
    server.use(http.get("*/treinador/leads/lead-1", () => HttpResponse.json({ title: "Não encontrado" }, { status: 404 })));
    await renderPage();

    expect(await screen.findByRole("button", { name: /tentar novamente/i })).toBeInTheDocument();
  });
});
