import { describe, it, expect, beforeEach, vi } from "vitest";
import { screen, fireEvent, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { renderWithProviders } from "@/test/render";
import SuporteForm from "../SuporteForm";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
  useParams: () => ({}),
}));

beforeEach(() => {
  server.use(
    http.get("*/conta/perfil", () =>
      HttpResponse.json({ nome: "João Teste", email: "joao@teste.com", tipoConta: "Aluno" }),
    ),
  );
});

async function preencherEnviar() {
  fireEvent.change(await screen.findByLabelText(/assunto/i), { target: { value: "Não consigo acessar" } });
  fireEvent.change(screen.getByLabelText(/descrição/i), {
    target: { value: "Descrição longa o suficiente para passar na validação." },
  });
  fireEvent.click(screen.getByRole("button", { name: /enviar mensagem/i }));
}

describe("SuporteForm", () => {
  it("pré-preenche nome/e-mail do cadastro como campos desabilitados", async () => {
    renderWithProviders(<SuporteForm />);
    const nome = (await screen.findByLabelText("Nome")) as HTMLInputElement;
    const email = screen.getByLabelText("E-mail") as HTMLInputElement;
    expect(nome.value).toBe("João Teste");
    expect(email.value).toBe("joao@teste.com");
    expect(nome).toBeDisabled();
    expect(email).toBeDisabled();
  });

  it("não envia e mostra erro de validação quando assunto é curto", async () => {
    let chamado = false;
    server.use(http.post("*/suporte/mensagens", () => { chamado = true; return new HttpResponse(null, { status: 202 }); }));
    renderWithProviders(<SuporteForm />);
    fireEvent.change(await screen.findByLabelText(/assunto/i), { target: { value: "ab" } });
    fireEvent.change(screen.getByLabelText(/descrição/i), {
      target: { value: "Descrição longa o suficiente para passar." },
    });
    fireEvent.click(screen.getByRole("button", { name: /enviar mensagem/i }));
    expect(await screen.findByText(/ao menos 3 caracteres/i)).toBeInTheDocument();
    expect(chamado).toBe(false);
  });

  it("envia apenas categoria/assunto/descrição (sem nome/e-mail) e mostra sucesso", async () => {
    let body: Record<string, unknown> | null = null;
    server.use(
      http.post("*/suporte/mensagens", async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return new HttpResponse(null, { status: 202 });
      }),
    );
    renderWithProviders(<SuporteForm />);
    await preencherEnviar();
    await waitFor(() => expect(body).not.toBeNull());
    expect(body).toEqual({
      categoria: "Duvida",
      assunto: "Não consigo acessar",
      descricao: "Descrição longa o suficiente para passar na validação.",
    });
    expect(await screen.findByText(/mensagem enviada/i)).toBeInTheDocument();
  });

  it("mostra banner de erro quando o envio falha", async () => {
    server.use(http.post("*/suporte/mensagens", () => new HttpResponse(null, { status: 422 })));
    renderWithProviders(<SuporteForm />);
    await preencherEnviar();
    expect(await screen.findByText(/não foi possível enviar/i)).toBeInTheDocument();
  });

  it("mostra erro quando o perfil não carrega e fecha o banner ao clicar", async () => {
    server.use(http.get("*/conta/perfil", () => new HttpResponse(null, { status: 500 })));
    renderWithProviders(<SuporteForm />);
    expect(await screen.findByText(/não foi possível carregar seus dados/i)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /fechar/i }));
    await waitFor(() =>
      expect(screen.queryByText(/não foi possível carregar seus dados/i)).not.toBeInTheDocument(),
    );
  });

  it("permite enviar outra mensagem após o sucesso", async () => {
    server.use(http.post("*/suporte/mensagens", () => new HttpResponse(null, { status: 202 })));
    renderWithProviders(<SuporteForm />);
    await preencherEnviar();
    expect(await screen.findByText(/mensagem enviada/i)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /enviar outra mensagem/i }));
    expect(await screen.findByLabelText(/assunto/i)).toBeInTheDocument();
  });
});
