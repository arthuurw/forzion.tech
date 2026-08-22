import { describe, it, expect } from "vitest";
import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";

async function renderPage() {
  const { default: Page } = await import("@/app/(treinador)/treinador/perfil-publico/page");
  render(<Page />);
}

describe("PerfilPublicoTreinadorPage", () => {
  it("exibe formulário vazio e toggle 'Publicado' desligado na 1ª visita", async () => {
    server.use(http.get("*/treinador/perfil-publico", () => HttpResponse.json(null)));
    await renderPage();

    expect(await screen.findByLabelText(/nome fantasia/i)).toHaveValue("");
    expect(screen.getByRole("switch", { name: /publicado \(visível ao agente conversacional\)/i })).not.toBeChecked();
    expect(screen.getByText(/nenhum horário cadastrado/i)).toBeInTheDocument();
    expect(screen.getByText(/nenhuma política cadastrada/i)).toBeInTheDocument();
  });

  it("bloqueia ligar 'Publicado' sem nome fantasia preenchido", async () => {
    server.use(http.get("*/treinador/perfil-publico", () => HttpResponse.json(null)));
    await renderPage();

    await screen.findByLabelText(/nome fantasia/i);
    fireEvent.click(screen.getByRole("switch", { name: /publicado \(visível ao agente conversacional\)/i }));

    expect(screen.getByText(/nome fantasia é obrigatório para publicar o perfil/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^salvar$/i })).toBeDisabled();
  });

  it("permite múltiplos horários no mesmo dia da semana", async () => {
    server.use(http.get("*/treinador/perfil-publico", () => HttpResponse.json(null)));
    await renderPage();

    await screen.findByLabelText(/nome fantasia/i);
    const abre = screen.getByLabelText(/abre às/i);
    const fecha = screen.getByLabelText(/fecha às/i);

    fireEvent.change(abre, { target: { value: "08:00" } });
    fireEvent.change(fecha, { target: { value: "12:00" } });
    fireEvent.click(screen.getByRole("button", { name: /^adicionar horário$/i }));

    fireEvent.change(abre, { target: { value: "14:00" } });
    fireEvent.change(fecha, { target: { value: "18:00" } });
    fireEvent.click(screen.getByRole("button", { name: /^adicionar horário$/i }));

    expect(screen.getByText("08:00 — 12:00")).toBeInTheDocument();
    expect(screen.getByText("14:00 — 18:00")).toBeInTheDocument();
  });

  it("bloqueia adicionar horário quando AbreAs >= FechaAs", async () => {
    server.use(http.get("*/treinador/perfil-publico", () => HttpResponse.json(null)));
    await renderPage();

    await screen.findByLabelText(/nome fantasia/i);
    fireEvent.change(screen.getByLabelText(/abre às/i), { target: { value: "10:00" } });
    fireEvent.change(screen.getByLabelText(/fecha às/i), { target: { value: "09:00" } });
    fireEvent.click(screen.getByRole("button", { name: /^adicionar horário$/i }));

    expect(screen.getByText(/horário de abertura deve ser antes do horário de fechamento/i)).toBeInTheDocument();
    expect(screen.getByText(/nenhum horário cadastrado/i)).toBeInTheDocument();
  });

  it("desligar 'Publicado' preserva os dados já preenchidos no form", async () => {
    server.use(http.get("*/treinador/perfil-publico", () => HttpResponse.json(null)));
    await renderPage();

    const nomeFantasia = await screen.findByLabelText(/nome fantasia/i);
    fireEvent.change(nomeFantasia, { target: { value: "Studio da Maria" } });

    const toggle = screen.getByRole("switch", { name: /publicado \(visível ao agente conversacional\)/i });
    fireEvent.click(toggle);
    expect(toggle).toBeChecked();

    fireEvent.click(toggle);
    expect(toggle).not.toBeChecked();
    expect(nomeFantasia).toHaveValue("Studio da Maria");
  });

  it("carrega dados existentes e envia payload ao salvar", async () => {
    let corpoEnviado: Record<string, unknown> | null = null;
    server.use(
      http.get("*/treinador/perfil-publico", () => HttpResponse.json({
        nomeFantasia: "Studio Teste",
        endereco: null,
        horariosFuncionamento: [],
        politicas: null,
        isPublicado: false,
      })),
      http.put("*/treinador/perfil-publico", async ({ request }) => {
        corpoEnviado = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({
          nomeFantasia: "Studio Teste", endereco: null, horariosFuncionamento: [], politicas: null, isPublicado: true,
        });
      }),
    );
    await renderPage();

    expect(await screen.findByLabelText(/nome fantasia/i)).toHaveValue("Studio Teste");
    fireEvent.click(screen.getByRole("switch", { name: /publicado \(visível ao agente conversacional\)/i }));
    fireEvent.click(screen.getByRole("button", { name: /^salvar$/i }));

    await waitFor(() => expect(corpoEnviado).not.toBeNull());
    expect(corpoEnviado).toMatchObject({ nomeFantasia: "Studio Teste", isPublicado: true });
  });
});
