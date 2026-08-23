import { describe, it, expect } from "vitest";
import { screen, waitFor, fireEvent, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { renderWithProviders } from "@/test/render";

async function renderPage() {
  const { default: Page } = await import("@/app/(treinador)/treinador/agenda/page");
  return renderWithProviders(<Page />, { skipAuth: true });
}

describe("AgendaTreinadorPage", () => {
  it("lista bloqueios separando pontual de recorrente", async () => {
    server.use(
      http.get("*/treinador/agenda/bloqueios", () =>
        HttpResponse.json([
          {
            id: "b1", tipo: "Pontual",
            inicioUtc: "2026-09-01T10:00:00.000Z", fimUtc: "2026-09-01T11:00:00.000Z",
            diaSemana: null, horaInicio: null, horaFim: null, motivo: null, createdAt: "2026-08-01T00:00:00.000Z",
          },
          {
            id: "b2", tipo: "RecorrenteSemanal",
            inicioUtc: null, fimUtc: null,
            diaSemana: 1, horaInicio: "12:00:00", horaFim: "13:00:00", motivo: null, createdAt: "2026-08-01T00:00:00.000Z",
          },
        ])),
    );
    await renderPage();

    expect(await screen.findByText(/01\/09\/2026/)).toBeInTheDocument();
    expect(screen.getByText("Segunda")).toBeInTheDocument();
    expect(screen.getByText("12:00 — 13:00")).toBeInTheDocument();
  });

  it("exibe mensagens de lista vazia para cada seção", async () => {
    server.use(http.get("*/treinador/agenda/bloqueios", () => HttpResponse.json([])));
    await renderPage();

    expect(await screen.findByText(/nenhum bloqueio pontual cadastrado/i)).toBeInTheDocument();
    expect(screen.getByText(/nenhum bloqueio recorrente cadastrado/i)).toBeInTheDocument();
  });

  it("cria bloqueio pontual e o lista sem recarregar a página", async () => {
    const user = userEvent.setup();
    let corpoEnviado: Record<string, unknown> | null = null;
    server.use(
      http.get("*/treinador/agenda/bloqueios", () => HttpResponse.json([])),
      http.post("*/treinador/agenda/bloqueios", async ({ request }) => {
        corpoEnviado = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(
          {
            id: "novo-bloqueio", tipo: "Pontual",
            inicioUtc: "2026-09-01T10:00:00.000Z", fimUtc: "2026-09-01T11:00:00.000Z",
            diaSemana: null, horaInicio: null, horaFim: null, motivo: null, createdAt: "2026-08-20T00:00:00.000Z",
          },
          { status: 201 },
        );
      }),
    );
    await renderPage();
    await screen.findByText(/nenhum bloqueio pontual cadastrado/i);

    const group = screen.getByRole("group", { name: /^data/i });
    await user.click(within(group).getByRole("spinbutton", { name: /day/i }));
    await user.keyboard("01092026");
    fireEvent.change(screen.getByLabelText(/^início$/i), { target: { value: "10:00" } });
    fireEvent.change(screen.getByLabelText(/^fim$/i), { target: { value: "11:00" } });
    fireEvent.click(screen.getByRole("button", { name: /^adicionar$/i }));

    await waitFor(() => expect(corpoEnviado).not.toBeNull());
    expect(corpoEnviado).toMatchObject({
      tipo: "Pontual",
      inicioUtc: "2026-09-01T10:00:00.000Z",
      fimUtc: "2026-09-01T11:00:00.000Z",
    });
    expect(await screen.findByText(/01\/09\/2026/)).toBeInTheDocument();
    expect(screen.queryByText(/nenhum bloqueio pontual cadastrado/i)).not.toBeInTheDocument();
  });

  it("falha ao criar bloqueio exibe erro e não adiciona item otimista à lista", async () => {
    const user = userEvent.setup();
    server.use(
      http.get("*/treinador/agenda/bloqueios", () => HttpResponse.json([])),
      http.post("*/treinador/agenda/bloqueios", () => HttpResponse.json({ detail: "Falha ao criar." }, { status: 400 })),
    );
    await renderPage();
    await screen.findByText(/nenhum bloqueio pontual cadastrado/i);

    const group = screen.getByRole("group", { name: /^data/i });
    await user.click(within(group).getByRole("spinbutton", { name: /day/i }));
    await user.keyboard("01092026");
    fireEvent.change(screen.getByLabelText(/^início$/i), { target: { value: "10:00" } });
    fireEvent.change(screen.getByLabelText(/^fim$/i), { target: { value: "11:00" } });
    fireEvent.click(screen.getByRole("button", { name: /^adicionar$/i }));

    expect(await screen.findByText(/falha ao criar/i)).toBeInTheDocument();
    expect(screen.getByText(/nenhum bloqueio pontual cadastrado/i)).toBeInTheDocument();
  });

  it("apaga bloqueio pontual e remove da lista", async () => {
    server.use(
      http.get("*/treinador/agenda/bloqueios", () =>
        HttpResponse.json([
          {
            id: "b1", tipo: "Pontual",
            inicioUtc: "2026-09-01T10:00:00.000Z", fimUtc: "2026-09-01T11:00:00.000Z",
            diaSemana: null, horaInicio: null, horaFim: null, motivo: null, createdAt: "2026-08-01T00:00:00.000Z",
          },
        ])),
      http.delete("*/treinador/agenda/bloqueios/b1", () => new HttpResponse(null, { status: 204 })),
    );
    await renderPage();

    const item = await screen.findByText(/01\/09\/2026/);
    expect(item).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /remover bloqueio pontual/i }));

    await waitFor(() => expect(screen.queryByText(/01\/09\/2026/)).not.toBeInTheDocument());
    expect(await screen.findByText(/nenhum bloqueio pontual cadastrado/i)).toBeInTheDocument();
  });

  it("falha ao apagar bloqueio exibe erro e mantém o item na lista", async () => {
    server.use(
      http.get("*/treinador/agenda/bloqueios", () =>
        HttpResponse.json([
          {
            id: "b1", tipo: "Pontual",
            inicioUtc: "2026-09-01T10:00:00.000Z", fimUtc: "2026-09-01T11:00:00.000Z",
            diaSemana: null, horaInicio: null, horaFim: null, motivo: null, createdAt: "2026-08-01T00:00:00.000Z",
          },
        ])),
      http.delete("*/treinador/agenda/bloqueios/b1", () => HttpResponse.json({ detail: "Não foi possível apagar." }, { status: 500 })),
    );
    await renderPage();

    await screen.findByText(/01\/09\/2026/);
    fireEvent.click(screen.getByRole("button", { name: /remover bloqueio pontual/i }));

    expect(await screen.findByText(/não foi possível apagar/i)).toBeInTheDocument();
    expect(screen.getByText(/01\/09\/2026/)).toBeInTheDocument();
  });
});
