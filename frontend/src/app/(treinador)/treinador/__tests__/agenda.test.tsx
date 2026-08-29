import { describe, it, expect } from "vitest";
import { screen, waitFor, fireEvent, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { renderWithProviders } from "@/test/render";

const POLITICA_PADRAO = { antecedenciaMinimaHoras: 2, horizonteDias: 60 };

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
      http.get("*/treinador/agenda/politica", () => HttpResponse.json(POLITICA_PADRAO)),
    );
    await renderPage();

    expect(await screen.findByText(/01\/09\/2026/)).toBeInTheDocument();
    expect(screen.getByText("12:00 — 13:00")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /remover bloqueio recorrente segunda 12:00/i })).toBeInTheDocument();
  });

  it("exibe mensagens de lista vazia para cada seção", async () => {
    server.use(
      http.get("*/treinador/agenda/bloqueios", () => HttpResponse.json([])),
      http.get("*/treinador/agenda/politica", () => HttpResponse.json(POLITICA_PADRAO)),
    );
    await renderPage();

    expect(await screen.findByText(/nenhum bloqueio pontual cadastrado/i)).toBeInTheDocument();
    expect(screen.getByText(/nenhum bloqueio recorrente cadastrado/i)).toBeInTheDocument();
  });

  it("cria bloqueio pontual e o lista sem recarregar a página", async () => {
    const user = userEvent.setup();
    let corpoEnviado: Record<string, unknown> | null = null;
    server.use(
      http.get("*/treinador/agenda/bloqueios", () => HttpResponse.json([])),
      http.get("*/treinador/agenda/politica", () => HttpResponse.json(POLITICA_PADRAO)),
      http.post("*/treinador/agenda/bloqueios", async ({ request }) => {
        corpoEnviado = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(
          {
            id: "novo-bloqueio", tipo: "Pontual",
            inicioUtc: "2026-09-01T13:00:00.000Z", fimUtc: "2026-09-01T14:00:00.000Z",
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
    fireEvent.change(screen.getAllByLabelText(/^início$/i)[0], { target: { value: "10:00" } });
    fireEvent.change(screen.getAllByLabelText(/^fim$/i)[0], { target: { value: "11:00" } });
    fireEvent.click(screen.getAllByRole("button", { name: /^adicionar$/i })[0]);

    await waitFor(() => expect(corpoEnviado).not.toBeNull());
    expect(corpoEnviado).toMatchObject({ tipo: "Pontual" });
    expect(await screen.findByText(/01\/09\/2026/)).toBeInTheDocument();
    expect(screen.queryByText(/nenhum bloqueio pontual cadastrado/i)).not.toBeInTheDocument();
  });

  it("converte o horário local do treinador (10:00–11:00 em São Paulo) para o instante UTC correspondente", async () => {
    const user = userEvent.setup();
    const tzOriginal = process.env.TZ;
    process.env.TZ = "America/Sao_Paulo";
    let corpoEnviado: Record<string, unknown> | null = null;
    try {
      server.use(
        http.get("*/treinador/agenda/bloqueios", () => HttpResponse.json([])),
        http.get("*/treinador/agenda/politica", () => HttpResponse.json(POLITICA_PADRAO)),
        http.post("*/treinador/agenda/bloqueios", async ({ request }) => {
          corpoEnviado = (await request.json()) as Record<string, unknown>;
          return HttpResponse.json(
            {
              id: "novo-bloqueio", tipo: "Pontual",
              inicioUtc: "2026-09-01T13:00:00.000Z", fimUtc: "2026-09-01T14:00:00.000Z",
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
      fireEvent.change(screen.getAllByLabelText(/^início$/i)[0], { target: { value: "10:00" } });
      fireEvent.change(screen.getAllByLabelText(/^fim$/i)[0], { target: { value: "11:00" } });
      fireEvent.click(screen.getAllByRole("button", { name: /^adicionar$/i })[0]);

      await waitFor(() => expect(corpoEnviado).not.toBeNull());
      expect(corpoEnviado).toMatchObject({
        inicioUtc: "2026-09-01T13:00:00.000Z",
        fimUtc: "2026-09-01T14:00:00.000Z",
      });
    } finally {
      process.env.TZ = tzOriginal;
    }
  });

  it("falha ao criar bloqueio exibe erro e não adiciona item otimista à lista", async () => {
    const user = userEvent.setup();
    server.use(
      http.get("*/treinador/agenda/bloqueios", () => HttpResponse.json([])),
      http.get("*/treinador/agenda/politica", () => HttpResponse.json(POLITICA_PADRAO)),
      http.post("*/treinador/agenda/bloqueios", () => HttpResponse.json({ detail: "Falha ao criar." }, { status: 400 })),
    );
    await renderPage();
    await screen.findByText(/nenhum bloqueio pontual cadastrado/i);

    const group = screen.getByRole("group", { name: /^data/i });
    await user.click(within(group).getByRole("spinbutton", { name: /day/i }));
    await user.keyboard("01092026");
    fireEvent.change(screen.getAllByLabelText(/^início$/i)[0], { target: { value: "10:00" } });
    fireEvent.change(screen.getAllByLabelText(/^fim$/i)[0], { target: { value: "11:00" } });
    fireEvent.click(screen.getAllByRole("button", { name: /^adicionar$/i })[0]);

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
      http.get("*/treinador/agenda/politica", () => HttpResponse.json(POLITICA_PADRAO)),
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
      http.get("*/treinador/agenda/politica", () => HttpResponse.json(POLITICA_PADRAO)),
      http.delete("*/treinador/agenda/bloqueios/b1", () => HttpResponse.json({ detail: "Não foi possível apagar." }, { status: 500 })),
    );
    await renderPage();

    await screen.findByText(/01\/09\/2026/);
    fireEvent.click(screen.getByRole("button", { name: /remover bloqueio pontual/i }));

    expect(await screen.findByText(/não foi possível apagar/i)).toBeInTheDocument();
    expect(screen.getByText(/01\/09\/2026/)).toBeInTheDocument();
  });

  it("cria bloqueio recorrente e o lista sem recarregar a página", async () => {
    let corpoEnviado: Record<string, unknown> | null = null;
    server.use(
      http.get("*/treinador/agenda/bloqueios", () => HttpResponse.json([])),
      http.get("*/treinador/agenda/politica", () => HttpResponse.json(POLITICA_PADRAO)),
      http.post("*/treinador/agenda/bloqueios", async ({ request }) => {
        corpoEnviado = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(
          {
            id: "novo-recorrente", tipo: "RecorrenteSemanal",
            inicioUtc: null, fimUtc: null,
            diaSemana: 1, horaInicio: "12:00:00", horaFim: "13:00:00", motivo: null, createdAt: "2026-08-20T00:00:00.000Z",
          },
          { status: 201 },
        );
      }),
    );
    await renderPage();
    await screen.findByText(/nenhum bloqueio recorrente cadastrado/i);

    fireEvent.change(screen.getAllByLabelText(/^início$/i)[1], { target: { value: "12:00" } });
    fireEvent.change(screen.getAllByLabelText(/^fim$/i)[1], { target: { value: "13:00" } });
    fireEvent.click(screen.getAllByRole("button", { name: /^adicionar$/i })[1]);

    await waitFor(() => expect(corpoEnviado).not.toBeNull());
    expect(corpoEnviado).toMatchObject({
      tipo: "RecorrenteSemanal",
      diaSemana: 1,
      horaInicio: "12:00",
      horaFim: "13:00",
    });
    expect(await screen.findByText("12:00 — 13:00")).toBeInTheDocument();
    expect(screen.queryByText(/nenhum bloqueio recorrente cadastrado/i)).not.toBeInTheDocument();
  });

  it("bloqueia adicionar bloqueio recorrente quando início >= fim, sem chamar a API", async () => {
    server.use(
      http.get("*/treinador/agenda/bloqueios", () => HttpResponse.json([])),
      http.get("*/treinador/agenda/politica", () => HttpResponse.json(POLITICA_PADRAO)),
    );
    await renderPage();
    await screen.findByText(/nenhum bloqueio recorrente cadastrado/i);

    fireEvent.change(screen.getAllByLabelText(/^início$/i)[1], { target: { value: "13:00" } });
    fireEvent.change(screen.getAllByLabelText(/^fim$/i)[1], { target: { value: "12:00" } });
    fireEvent.click(screen.getAllByRole("button", { name: /^adicionar$/i })[1]);

    expect(screen.getByText(/horário de início deve ser antes do horário de fim/i)).toBeInTheDocument();
    expect(screen.getByText(/nenhum bloqueio recorrente cadastrado/i)).toBeInTheDocument();
  });

  it("apaga bloqueio recorrente e remove da lista", async () => {
    server.use(
      http.get("*/treinador/agenda/bloqueios", () =>
        HttpResponse.json([
          {
            id: "r1", tipo: "RecorrenteSemanal",
            inicioUtc: null, fimUtc: null,
            diaSemana: 1, horaInicio: "12:00:00", horaFim: "13:00:00", motivo: null, createdAt: "2026-08-01T00:00:00.000Z",
          },
        ])),
      http.get("*/treinador/agenda/politica", () => HttpResponse.json(POLITICA_PADRAO)),
      http.delete("*/treinador/agenda/bloqueios/r1", () => new HttpResponse(null, { status: 204 })),
    );
    await renderPage();

    await screen.findByText("12:00 — 13:00");
    fireEvent.click(screen.getByRole("button", { name: /remover bloqueio recorrente/i }));

    await waitFor(() => expect(screen.queryByText("12:00 — 13:00")).not.toBeInTheDocument());
    expect(await screen.findByText(/nenhum bloqueio recorrente cadastrado/i)).toBeInTheDocument();
  });

  it("carrega a política existente e exibe nos campos", async () => {
    server.use(
      http.get("*/treinador/agenda/bloqueios", () => HttpResponse.json([])),
      http.get("*/treinador/agenda/politica", () => HttpResponse.json({ antecedenciaMinimaHoras: 6, horizonteDias: 45 })),
    );
    await renderPage();
    await screen.findByText(/nenhum bloqueio pontual cadastrado/i);
    fireEvent.click(screen.getByRole("tab", { name: /^política$/i }));

    expect(await screen.findByLabelText(/antecedência mínima/i)).toHaveValue(6);
    expect(screen.getByLabelText(/horizonte/i)).toHaveValue(45);
  });

  it("salva a política e reflete os valores persistidos após recarregar", async () => {
    let politicaAtual = { ...POLITICA_PADRAO };
    server.use(
      http.get("*/treinador/agenda/bloqueios", () => HttpResponse.json([])),
      http.get("*/treinador/agenda/politica", () => HttpResponse.json(politicaAtual)),
      http.put("*/treinador/agenda/politica", async ({ request }) => {
        politicaAtual = (await request.json()) as { antecedenciaMinimaHoras: number; horizonteDias: number };
        return HttpResponse.json(politicaAtual);
      }),
    );
    const { unmount } = await renderPage();
    await screen.findByText(/nenhum bloqueio pontual cadastrado/i);
    fireEvent.click(screen.getByRole("tab", { name: /^política$/i }));
    await screen.findByLabelText(/antecedência mínima/i);

    fireEvent.change(screen.getByLabelText(/antecedência mínima/i), { target: { value: "4" } });
    fireEvent.change(screen.getByLabelText(/horizonte/i), { target: { value: "30" } });
    fireEvent.click(screen.getByRole("button", { name: /^salvar política$/i }));

    await waitFor(() => expect(politicaAtual).toEqual({ antecedenciaMinimaHoras: 4, horizonteDias: 30 }));

    unmount();
    await renderPage();
    await screen.findByText(/nenhum bloqueio pontual cadastrado/i);
    fireEvent.click(screen.getByRole("tab", { name: /^política$/i }));

    expect(await screen.findByLabelText(/antecedência mínima/i)).toHaveValue(4);
    expect(screen.getByLabelText(/horizonte/i)).toHaveValue(30);
  });

  it("oferece as três abas Bloqueios, Política e Solicitações", async () => {
    server.use(
      http.get("*/treinador/agenda/bloqueios", () => HttpResponse.json([])),
      http.get("*/treinador/agenda/politica", () => HttpResponse.json(POLITICA_PADRAO)),
    );
    await renderPage();
    await screen.findByText(/nenhum bloqueio pontual cadastrado/i);

    expect(screen.getByRole("tab", { name: /^bloqueios$/i })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: /^política$/i })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: /^solicitações$/i })).toBeInTheDocument();
  });

  it("troca para a aba Solicitações e carrega a lista", async () => {
    server.use(
      http.get("*/treinador/agenda/bloqueios", () => HttpResponse.json([])),
      http.get("*/treinador/agenda/politica", () => HttpResponse.json(POLITICA_PADRAO)),
      http.get("*/treinador/agenda/solicitacoes", () =>
        HttpResponse.json({
          items: [
            {
              id: "s1", pacoteId: "p1", pacoteNome: "Aula experimental",
              inicioUtc: "2026-09-01T13:00:00.000Z", fimUtc: "2026-09-01T14:00:00.000Z",
              status: "PendenteAgente", motivo: null, createdAt: "2026-08-20T00:00:00.000Z",
              leadId: "l1", leadNome: "Maria Silva", leadContatoTipo: "Email", leadContatoValor: "maria@exemplo.com",
              leadAnonimizado: false,
            },
          ],
          total: 1, pagina: 1, tamanhoPagina: 20,
        })),
    );
    await renderPage();
    await screen.findByText(/nenhum bloqueio pontual cadastrado/i);

    fireEvent.click(screen.getByRole("tab", { name: /^solicitações$/i }));

    expect(await screen.findByText("Aula experimental")).toBeInTheDocument();
    expect(screen.getByText("Maria Silva")).toBeInTheDocument();
  });
});
