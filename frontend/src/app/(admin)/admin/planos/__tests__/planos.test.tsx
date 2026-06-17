/**
 * CRUD da página /admin/planos: listagem (vazia/preenchida/erro), criar,
 * editar e excluir — sucesso e erro.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, fireEvent, waitFor, within } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
  useParams: () => ({}),
}));

import PlanosAdminPage from "../page";
import { renderWithProviders } from "@/test/render";

const PLANO = {
  planoId: "pl1",
  nome: "Basic",
  tier: "Basic",
  maxAlunos: 10,
  preco: 49.9,
  descricao: "Plano basic",
  isAtivo: true,
};

beforeEach(() => {
  server.use(
    http.get("*/api/auth/me", () =>
      HttpResponse.json({ contaId: "c1", tipoConta: "SystemAdmin", perfilId: "p1" }),
    ),
    http.get("*/admin/planos", () => HttpResponse.json([PLANO])),
  );
});

describe("/admin/planos — listagem", () => {
  it("renderiza planos carregados", async () => {
    renderWithProviders(<PlanosAdminPage />);
    expect(await screen.findByRole("heading", { name: "Basic" })).toBeInTheDocument();
    expect(screen.getByText(/Até 10 alunos/i)).toBeInTheDocument();
  });

  it("estado vazio mostra EmptyState", async () => {
    server.use(http.get("*/admin/planos", () => HttpResponse.json([])));
    renderWithProviders(<PlanosAdminPage />);
    expect(await screen.findByText(/nenhum plano cadastrado/i)).toBeInTheDocument();
  });

  it("erro ao carregar mostra banner", async () => {
    server.use(http.get("*/admin/planos", () => new HttpResponse(null, { status: 500 })));
    renderWithProviders(<PlanosAdminPage />);
    expect(await screen.findByText(/erro ao carregar planos/i)).toBeInTheDocument();
  });
});

describe("/admin/planos — criar", () => {
  it("cria plano com sucesso", async () => {
    let posted: unknown = null;
    server.use(
      http.post("*/admin/planos", async ({ request }) => {
        posted = await request.json();
        return HttpResponse.json({ ...PLANO, planoId: "pl2", nome: "Pro" });
      }),
    );
    renderWithProviders(<PlanosAdminPage />);
    await screen.findByText(/Até 10 alunos/i);

    fireEvent.click(screen.getByRole("button", { name: /novo plano/i }));
    const dialog = await screen.findByRole("dialog");
    fireEvent.change(within(dialog).getByLabelText(/nome/i), { target: { value: "Pro" } });
    fireEvent.change(within(dialog).getByLabelText(/máximo de alunos/i), { target: { value: "50" } });
    fireEvent.change(within(dialog).getByLabelText(/preço/i), { target: { value: "99.9" } });
    fireEvent.change(within(dialog).getByLabelText(/descrição/i), { target: { value: "Tudo do Basic +" } });
    fireEvent.click(within(dialog).getByRole("button", { name: /^criar$/i }));

    await waitFor(() =>
      expect(posted).toMatchObject({ nome: "Pro", maxAlunos: 50, preco: 99.9 }),
    );
    expect(await screen.findByText(/criado/i)).toBeInTheDocument();
  });

  it("erro ao criar mostra banner", async () => {
    server.use(http.post("*/admin/planos", () => new HttpResponse(null, { status: 500 })));
    renderWithProviders(<PlanosAdminPage />);
    await screen.findByText(/Até 10 alunos/i);

    fireEvent.click(screen.getByRole("button", { name: /novo plano/i }));
    const dialog = await screen.findByRole("dialog");
    fireEvent.change(within(dialog).getByLabelText(/nome/i), { target: { value: "X" } });
    fireEvent.change(within(dialog).getByLabelText(/máximo de alunos/i), { target: { value: "5" } });
    fireEvent.change(within(dialog).getByLabelText(/preço/i), { target: { value: "0" } });
    fireEvent.click(within(dialog).getByRole("button", { name: /^criar$/i }));

    expect(await screen.findByText(/erro ao criar plano/i)).toBeInTheDocument();
  });
});

describe("/admin/planos — editar", () => {
  it("edita plano com sucesso", async () => {
    let patched: unknown = null;
    server.use(
      http.patch("*/admin/planos/pl1", async ({ request }) => {
        patched = await request.json();
        return HttpResponse.json({ ...PLANO, nome: "Basic+" });
      }),
    );
    renderWithProviders(<PlanosAdminPage />);
    await screen.findByText(/Até 10 alunos/i);

    // eslint-disable-next-line testing-library/no-node-access
    fireEvent.click(screen.getByTestId("EditIcon").closest("button")!);
    const dialog = await screen.findByRole("dialog");
    const nomeInput = within(dialog).getByLabelText(/nome/i);
    fireEvent.change(nomeInput, { target: { value: "Basic+" } });
    fireEvent.click(within(dialog).getByRole("button", { name: /salvar/i }));

    await waitFor(() => expect(patched).toMatchObject({ nome: "Basic+" }));
    expect(await screen.findByText(/atualizado/i)).toBeInTheDocument();
  });

  it("erro ao editar mostra banner", async () => {
    server.use(http.patch("*/admin/planos/pl1", () => new HttpResponse(null, { status: 500 })));
    renderWithProviders(<PlanosAdminPage />);
    await screen.findByText(/Até 10 alunos/i);

    // eslint-disable-next-line testing-library/no-node-access
    fireEvent.click(screen.getByTestId("EditIcon").closest("button")!);
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: /salvar/i }));

    expect(await screen.findByText(/erro ao atualizar plano/i)).toBeInTheDocument();
  });
});

describe("/admin/planos — excluir", () => {
  it("exclui plano com sucesso", async () => {
    let deleted = false;
    server.use(
      http.delete("*/admin/planos/pl1", () => {
        deleted = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderWithProviders(<PlanosAdminPage />);
    await screen.findByText(/Até 10 alunos/i);

    // eslint-disable-next-line testing-library/no-node-access
    fireEvent.click(screen.getByTestId("DeleteIcon").closest("button")!);
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: /^excluir$/i }));

    await waitFor(() => expect(deleted).toBe(true));
    expect(await screen.findByText(/excluído/i)).toBeInTheDocument();
  });

  it("erro ao excluir mostra banner", async () => {
    server.use(http.delete("*/admin/planos/pl1", () => new HttpResponse(null, { status: 500 })));
    renderWithProviders(<PlanosAdminPage />);
    await screen.findByText(/Até 10 alunos/i);

    // eslint-disable-next-line testing-library/no-node-access
    fireEvent.click(screen.getByTestId("DeleteIcon").closest("button")!);
    const dialog = await screen.findByRole("dialog");
    fireEvent.click(within(dialog).getByRole("button", { name: /^excluir$/i }));

    expect(await screen.findByText(/erro ao excluir plano/i)).toBeInTheDocument();
  });
});
