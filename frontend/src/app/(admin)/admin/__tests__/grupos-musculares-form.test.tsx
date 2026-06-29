import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { renderWithProviders } from "@/test/render";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn() })),
  useParams: vi.fn(() => ({})),
}));

import GruposMuscularesAdminPage from "@/app/(admin)/admin/grupos-musculares/page";

const GRUPO = {
  id: "gm1",
  nome: "Peitoral",
  createdAt: "2024-01-01T00:00:00Z",
};

beforeEach(() => {
  server.use(
    http.get("*/api/auth/me", () =>
      HttpResponse.json({ contaId: "c1", tipoConta: "SystemAdmin", perfilId: "p1" }),
    ),
    http.get("*/admin/grupos-musculares", () => HttpResponse.json([GRUPO])),
  );
});

describe("GruposMuscularesAdminPage — criar form", () => {
  it("invalid: submit vazio não chama POST e mostra mensagem de validação", async () => {
    let captured: unknown = null;
    server.use(
      http.post("*/admin/grupos-musculares", async ({ request }) => {
        captured = await request.json();
        return HttpResponse.json(GRUPO, { status: 201 });
      }),
    );
    const user = userEvent.setup();
    renderWithProviders(<GruposMuscularesAdminPage />);
    await screen.findByText("Peitoral");

    await user.click(screen.getByRole("button", { name: /novo grupo/i }));
    await screen.findByRole("dialog");
    await user.click(screen.getByRole("button", { name: /adicionar/i }));

    await waitFor(() => {
      expect(screen.getByText("Informe o nome.")).toBeInTheDocument();
    });
    expect(captured).toBeNull();
  });

  it("valid: preenche nome e submete → POST capturado com { nome }", async () => {
    let captured: unknown = null;
    server.use(
      http.post("*/admin/grupos-musculares", async ({ request }) => {
        captured = await request.json();
        return HttpResponse.json({ ...GRUPO, nome: "Costas" }, { status: 201 });
      }),
    );
    const user = userEvent.setup();
    renderWithProviders(<GruposMuscularesAdminPage />);
    await screen.findByText("Peitoral");

    await user.click(screen.getByRole("button", { name: /novo grupo/i }));
    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByLabelText(/nome/i), "Costas");
    await user.click(within(dialog).getByRole("button", { name: /adicionar/i }));

    await waitFor(() => {
      expect(captured).toMatchObject({ nome: "Costas" });
    });
  });

  it("edit: abre edição de linha existente, altera nome e submete → PATCH capturado com { nome }", async () => {
    let patched: unknown = null;
    server.use(
      http.patch("*/admin/grupos-musculares/gm1", async ({ request }) => {
        patched = await request.json();
        return HttpResponse.json({ ...GRUPO, nome: "Bíceps" });
      }),
    );
    const user = userEvent.setup();
    renderWithProviders(<GruposMuscularesAdminPage />);
    await screen.findByText("Peitoral");

    await user.click(screen.getByRole("button", { name: /editar grupo muscular/i }));
    const dialog = await screen.findByRole("dialog");
    const nomeInput = within(dialog).getByLabelText(/nome/i);
    await user.clear(nomeInput);
    await user.type(nomeInput, "Bíceps");
    await user.click(within(dialog).getByRole("button", { name: /salvar/i }));

    await waitFor(() => {
      expect(patched).toMatchObject({ nome: "Bíceps" });
    });
  });
});
