import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type { ListarLeadsResponse, ObterMetricasLeadsResponse } from "@/types";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

function listResponse(): ListarLeadsResponse {
  return { items: [], total: 0, pagina: 1, tamanhoPagina: 20 };
}

function metricasResponse(): ObterMetricasLeadsResponse {
  return { total: 0, porOrigemAgente: 0, porOrigemManual: 0, convertidos: 0, taxaConversao: 0, porMotivoDescarte: {} };
}

async function renderPage() {
  const { default: Page } = await import("../page");
  render(<Page />);
}

describe("LeadsTreinadorPage — cadastro manual", () => {
  beforeEach(() => {
    server.use(
      http.get("*/treinador/leads", () => HttpResponse.json(listResponse())),
      http.get("*/treinador/leads/metricas", () => HttpResponse.json(metricasResponse())),
    );
  });

  it("cadastra um lead manual com dados válidos e recarrega a lista", async () => {
    let corpo: unknown = null;
    server.use(
      http.post("*/treinador/leads", async ({ request }) => {
        corpo = await request.json();
        return HttpResponse.json({ leadId: "novo-lead" }, { status: 201 });
      }),
    );
    await renderPage();

    fireEvent.click(await screen.findByRole("button", { name: "Novo lead" }));
    await screen.findByRole("dialog");

    fireEvent.change(screen.getByLabelText(/^nome/i), { target: { value: "João Pereira" } });
    fireEvent.change(screen.getByLabelText(/^contato/i), { target: { value: "joao@example.com" } });
    fireEvent.change(screen.getByLabelText(/finalidade do consentimento/i), { target: { value: "Contato comercial" } });
    fireEvent.click(screen.getByRole("button", { name: "Cadastrar" }));

    await waitFor(() => expect(corpo).toEqual({
      nome: "João Pereira",
      contatoTipo: "Email",
      contatoValor: "joao@example.com",
      interesse: null,
      consentimentoFinalidade: "Contato comercial",
    }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
  });

  it("bloqueia o submit com nome vazio", async () => {
    const post = vi.fn();
    server.use(http.post("*/treinador/leads", () => { post(); return HttpResponse.json({ leadId: "x" }, { status: 201 }); }));
    await renderPage();

    fireEvent.click(await screen.findByRole("button", { name: "Novo lead" }));
    await screen.findByRole("dialog");
    fireEvent.change(screen.getByLabelText(/^contato/i), { target: { value: "joao@example.com" } });
    fireEvent.change(screen.getByLabelText(/finalidade do consentimento/i), { target: { value: "Contato comercial" } });
    fireEvent.click(screen.getByRole("button", { name: "Cadastrar" }));

    expect(await screen.findByText(/informe o nome/i)).toBeInTheDocument();
    expect(post).not.toHaveBeenCalled();
  });

  it("valida e-mail inválido antes de enviar", async () => {
    const post = vi.fn();
    server.use(http.post("*/treinador/leads", () => { post(); return HttpResponse.json({ leadId: "x" }, { status: 201 }); }));
    await renderPage();

    fireEvent.click(await screen.findByRole("button", { name: "Novo lead" }));
    await screen.findByRole("dialog");
    fireEvent.change(screen.getByLabelText(/^nome/i), { target: { value: "João" } });
    fireEvent.change(screen.getByLabelText(/^contato/i), { target: { value: "not-an-email" } });
    fireEvent.change(screen.getByLabelText(/finalidade do consentimento/i), { target: { value: "Contato comercial" } });
    fireEvent.click(screen.getByRole("button", { name: "Cadastrar" }));

    expect(await screen.findByText(/e-mail inválido/i)).toBeInTheDocument();
    expect(post).not.toHaveBeenCalled();
  });

  it("erro do servidor é exibido no alerta", async () => {
    server.use(
      http.post("*/treinador/leads", () => HttpResponse.json({ detail: "Finalidade obrigatória." }, { status: 400 })),
    );
    await renderPage();

    fireEvent.click(await screen.findByRole("button", { name: "Novo lead" }));
    await screen.findByRole("dialog");
    fireEvent.change(screen.getByLabelText(/^nome/i), { target: { value: "João" } });
    fireEvent.change(screen.getByLabelText(/^contato/i), { target: { value: "joao@example.com" } });
    fireEvent.change(screen.getByLabelText(/finalidade do consentimento/i), { target: { value: "Contato comercial" } });
    fireEvent.click(screen.getByRole("button", { name: "Cadastrar" }));

    expect(await screen.findByText("Finalidade obrigatória.")).toBeInTheDocument();
  });

  it("estado vazio também abre o cadastro manual", async () => {
    await renderPage();
    fireEvent.click(await screen.findByRole("button", { name: "Cadastrar lead manualmente" }));
    expect(await screen.findByRole("dialog")).toBeInTheDocument();
  });
});
