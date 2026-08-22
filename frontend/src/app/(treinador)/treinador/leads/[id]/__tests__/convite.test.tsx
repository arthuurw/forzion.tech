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
    interesse: null,
    consentimentoFinalidade: "Contato comercial",
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

describe("DetalheLeadPage — convite", () => {
  it("caminho feliz: envia convite e sinaliza sucesso", async () => {
    server.use(http.get("*/treinador/leads/lead-1", () => HttpResponse.json(leadDetalhe())));
    const post = vi.fn();
    server.use(http.post("*/treinador/leads/lead-1/convite", () => {
      post();
      return HttpResponse.json({ enviado: true });
    }));
    await renderPage();

    fireEvent.click(await screen.findByRole("button", { name: "Enviar convite" }));
    await screen.findByRole("dialog");
    fireEvent.click(screen.getByRole("button", { name: "Enviar" }));

    await waitFor(() => expect(post).toHaveBeenCalledOnce());
    expect(await screen.findByText("Convite enviado.")).toBeInTheDocument();
    expect(await screen.findByRole("button", { name: "Reenviar convite" })).toBeInTheDocument();
  });

  it("contato do tipo telefone bloqueia o convite com o motivo visível", async () => {
    server.use(http.get("*/treinador/leads/lead-1", () => HttpResponse.json(leadDetalhe({ contatoTipo: "Telefone", contatoValor: "11987654321" }))));
    await renderPage();

    const botao = await screen.findByRole("button", { name: "Enviar convite" });
    expect(botao).toBeDisabled();
    expect(screen.getByText(/convite exige contato por e-mail ou whatsapp/i)).toBeInTheDocument();
  });

  it("falha no envio do e-mail mantém o convite e avisa na UI", async () => {
    server.use(http.get("*/treinador/leads/lead-1", () => HttpResponse.json(leadDetalhe())));
    server.use(http.post("*/treinador/leads/lead-1/convite", () => HttpResponse.json({ enviado: false })));
    await renderPage();

    fireEvent.click(await screen.findByRole("button", { name: "Enviar convite" }));
    await screen.findByRole("dialog");
    fireEvent.click(screen.getByRole("button", { name: "Enviar" }));

    expect(await screen.findByText(/convite gerado, mas o e-mail não saiu/i)).toBeInTheDocument();
  });

  it("reenvio: segunda emissão sinaliza que o link anterior será invalidado", async () => {
    server.use(http.get("*/treinador/leads/lead-1", () => HttpResponse.json(leadDetalhe())));
    let chamadas = 0;
    server.use(http.post("*/treinador/leads/lead-1/convite", () => {
      chamadas += 1;
      return HttpResponse.json({ enviado: true });
    }));
    await renderPage();

    fireEvent.click(await screen.findByRole("button", { name: "Enviar convite" }));
    await screen.findByRole("dialog");
    fireEvent.click(screen.getByRole("button", { name: "Enviar" }));
    await waitFor(() => expect(chamadas).toBe(1));

    fireEvent.click(await screen.findByRole("button", { name: "Reenviar convite" }));
    expect(await screen.findByText(/invalida o link anterior/i)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Enviar" }));

    await waitFor(() => expect(chamadas).toBe(2));
  });

  it("erro do servidor ao convidar é exibido no alerta", async () => {
    server.use(http.get("*/treinador/leads/lead-1", () => HttpResponse.json(leadDetalhe())));
    server.use(http.post("*/treinador/leads/lead-1/convite", () =>
      HttpResponse.json({ detail: "Este lead já foi finalizado." }, { status: 422 })));
    await renderPage();

    fireEvent.click(await screen.findByRole("button", { name: "Enviar convite" }));
    await screen.findByRole("dialog");
    fireEvent.click(screen.getByRole("button", { name: "Enviar" }));

    expect(await screen.findByText("Este lead já foi finalizado.")).toBeInTheDocument();
  });
});
