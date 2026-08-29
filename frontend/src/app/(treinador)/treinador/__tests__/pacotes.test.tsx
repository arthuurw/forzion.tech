import { describe, it, expect } from "vitest";
import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";

const PACOTE_PUBLICO = {
  pacoteId: "11111111-1111-1111-1111-111111111111",
  nome: "Pilates em grupo",
  descricao: "Aulas em grupo de pilates",
  preco: 150,
  treinadorId: "22222222-2222-2222-2222-222222222222",
  isAtivo: true,
  categoria: "Pilates",
  duracaoMinutos: 60,
  trialDisponivel: true,
  isPublico: true,
};

const PACOTE_PRIVADO = {
  ...PACOTE_PUBLICO,
  pacoteId: "33333333-3333-3333-3333-333333333333",
  nome: "Avaliação física",
  isPublico: false,
};

async function renderPage() {
  const { default: Page } = await import("@/app/(treinador)/treinador/pacotes/page");
  render(<Page />);
}

describe("PacotesTreinadorPage — catálogo público", () => {
  it("renderiza os campos novos no dialog de criação", async () => {
    server.use(http.get("*/treinador/pacotes", () => HttpResponse.json([])));
    await renderPage();

    fireEvent.click(await screen.findByRole("button", { name: /novo pacote/i }));

    expect(screen.getByLabelText(/^Categoria$/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Duração \(min\)/i)).toBeInTheDocument();
    expect(screen.getByRole("switch", { name: /aceita aula experimental/i })).toBeInTheDocument();
    expect(screen.getByRole("switch", { name: /público \(visível no catálogo do agente\)/i })).toBeInTheDocument();
  });

  it("bloqueia criar quando 'Público' está ligado sem categoria preenchida", async () => {
    server.use(http.get("*/treinador/pacotes", () => HttpResponse.json([])));
    await renderPage();

    fireEvent.click(await screen.findByRole("button", { name: /novo pacote/i }));
    fireEvent.change(screen.getByLabelText(/^Nome/i), { target: { value: "Funcional" } });
    fireEvent.change(screen.getByLabelText(/Preço mensal/i), { target: { value: "100" } });
    fireEvent.click(screen.getByRole("switch", { name: /público \(visível no catálogo do agente\)/i }));

    expect(screen.getByText(/categoria é obrigatória para tornar o pacote público/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^criar$/i })).toBeDisabled();
  });

  it("libera criar quando categoria é preenchida com 'Público' ligado", async () => {
    server.use(http.get("*/treinador/pacotes", () => HttpResponse.json([])));
    await renderPage();

    fireEvent.click(await screen.findByRole("button", { name: /novo pacote/i }));
    fireEvent.change(screen.getByLabelText(/^Nome/i), { target: { value: "Funcional" } });
    fireEvent.change(screen.getByLabelText(/Preço mensal/i), { target: { value: "100" } });
    fireEvent.click(screen.getByRole("switch", { name: /público \(visível no catálogo do agente\)/i }));
    fireEvent.change(screen.getByLabelText(/^Categoria$/i), { target: { value: "Funcional" } });

    expect(screen.queryByText(/categoria é obrigatória para tornar o pacote público/i)).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^criar$/i })).toBeEnabled();
  });

  it("envia os campos novos ao criar um pacote público", async () => {
    let corpoEnviado: Record<string, unknown> | null = null;
    server.use(
      http.get("*/treinador/pacotes", () => HttpResponse.json([])),
      http.post("*/treinador/pacotes", async ({ request }) => {
        corpoEnviado = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({ ...PACOTE_PUBLICO, pacoteId: "novo-id" }, { status: 201 });
      }),
    );
    await renderPage();

    fireEvent.click(await screen.findByRole("button", { name: /novo pacote/i }));
    fireEvent.change(screen.getByLabelText(/^Nome/i), { target: { value: "Pilates" } });
    fireEvent.change(screen.getByLabelText(/Preço mensal/i), { target: { value: "150" } });
    fireEvent.change(screen.getByLabelText(/^Categoria$/i), { target: { value: "Pilates" } });
    fireEvent.change(screen.getByLabelText(/Duração \(min\)/i), { target: { value: "60" } });
    fireEvent.click(screen.getByRole("switch", { name: /aceita aula experimental/i }));
    fireEvent.click(screen.getByRole("switch", { name: /público \(visível no catálogo do agente\)/i }));
    fireEvent.click(screen.getByRole("button", { name: /^criar$/i }));

    await waitFor(() => expect(corpoEnviado).not.toBeNull());
    expect(corpoEnviado).toMatchObject({
      categoria: "Pilates",
      duracaoMinutos: 60,
      trialDisponivel: true,
      isPublico: true,
    });
  });

  it("carrega pacote existente publico e reflete o toggle ligado no dialog de edição", async () => {
    server.use(http.get("*/treinador/pacotes", () => HttpResponse.json([PACOTE_PUBLICO])));
    await renderPage();

    fireEvent.click(await screen.findByText("Pilates em grupo"));

    expect(await screen.findByLabelText(/^Categoria$/i)).toHaveValue("Pilates");
    expect(screen.getByLabelText(/Duração \(min\)/i)).toHaveValue(60);
    expect(screen.getByRole("switch", { name: /aceita aula experimental/i })).toBeChecked();
    expect(screen.getByRole("switch", { name: /público \(visível no catálogo do agente\)/i })).toBeChecked();
  });

  it("exibe o marcador 'Público' no card de um pacote público", async () => {
    server.use(http.get("*/treinador/pacotes", () => HttpResponse.json([PACOTE_PUBLICO])));
    await renderPage();

    expect(await screen.findByText("Público")).toBeInTheDocument();
  });

  it("bloqueia salvar edição quando desliga categoria com 'Público' ligado", async () => {
    server.use(http.get("*/treinador/pacotes", () => HttpResponse.json([PACOTE_PUBLICO])));
    await renderPage();

    fireEvent.click(await screen.findByText("Pilates em grupo"));
    const categoriaInput = await screen.findByLabelText(/^Categoria$/i);
    fireEvent.change(categoriaInput, { target: { value: "" } });

    expect(screen.getByText(/categoria é obrigatória para tornar o pacote público/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^salvar$/i })).toBeDisabled();
  });

  it("limpa categoria e duração de um pacote privado ao esvaziar os campos e salvar", async () => {
    let corpoEnviado: Record<string, unknown> | null = null;
    server.use(
      http.get("*/treinador/pacotes", () => HttpResponse.json([PACOTE_PRIVADO])),
      http.patch("*/treinador/pacotes/:id", async ({ request }) => {
        corpoEnviado = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({ ...PACOTE_PRIVADO, categoria: null, duracaoMinutos: null });
      }),
    );
    await renderPage();

    fireEvent.click(await screen.findByText("Avaliação física"));
    fireEvent.change(await screen.findByLabelText(/^Categoria$/i), { target: { value: "" } });
    fireEvent.change(screen.getByLabelText(/Duração \(min\)/i), { target: { value: "" } });
    fireEvent.click(screen.getByRole("button", { name: /^salvar$/i }));

    await waitFor(() => expect(corpoEnviado).not.toBeNull());
    expect(corpoEnviado).toMatchObject({ categoria: "", duracaoMinutos: 0 });
  });

  it("preserva categoria e duração de um pacote privado quando os campos não são tocados", async () => {
    let corpoEnviado: Record<string, unknown> | null = null;
    server.use(
      http.get("*/treinador/pacotes", () => HttpResponse.json([PACOTE_PRIVADO])),
      http.patch("*/treinador/pacotes/:id", async ({ request }) => {
        corpoEnviado = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(PACOTE_PRIVADO);
      }),
    );
    await renderPage();

    fireEvent.click(await screen.findByText("Avaliação física"));
    await screen.findByLabelText(/^Categoria$/i);
    fireEvent.click(screen.getByRole("button", { name: /^salvar$/i }));

    await waitFor(() => expect(corpoEnviado).not.toBeNull());
    expect(corpoEnviado).toMatchObject({ categoria: "Pilates", duracaoMinutos: 60 });
  });
});
