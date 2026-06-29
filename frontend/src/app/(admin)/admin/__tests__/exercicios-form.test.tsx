import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor, within, fireEvent } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { renderWithProviders } from "@/test/render";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn() })),
  useParams: vi.fn(() => ({})),
}));

vi.mock("@/hooks/usePaginatedList", () => ({
  usePaginatedList: vi.fn(() => ({
    items: [],
    total: 0,
    page: 0,
    pageSize: 20,
    loading: false,
    error: "",
    success: "",
    setPage: vi.fn(),
    setPageSize: vi.fn(),
    setError: vi.fn(),
    setSuccess: vi.fn(),
    reload: vi.fn(),
  })),
}));

const GRUPO = { id: "gm-1", nome: "Peito", createdAt: "2025-01-01T00:00:00Z", updatedAt: null };

describe("ExerciciosAdminPage — criar (RHF + Zod)", () => {
  let captured: unknown = null;

  beforeEach(() => {
    captured = null;
    server.use(
      http.get("*/admin/grupos-musculares", () => HttpResponse.json([GRUPO])),
      http.get("*/admin/exercicios", () =>
        HttpResponse.json({ items: [], total: 0, pagina: 1, tamanhoPagina: 20 }),
      ),
      http.post("*/admin/exercicios", async ({ request }) => {
        captured = await request.json();
        return HttpResponse.json(
          {
            exercicioId: "ex-1",
            nome: "Supino",
            grupoMuscularId: "gm-1",
            descricao: null,
            grupoMuscular: "Peito",
            isGlobal: true,
            treinadorId: null,
          },
          { status: 201 },
        );
      }),
    );
  });

  it("submit sem nome → erro de validação exibido, POST não chamado", async () => {
    const user = userEvent.setup();
    const { default: Page } = await import("@/app/(admin)/admin/exercicios/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await user.click(await screen.findByRole("button", { name: /novo exercício/i }));
    const dialog = await screen.findByRole("dialog");

    await user.click(within(dialog).getByRole("button", { name: /^adicionar$/i }));

    await waitFor(() => {
      expect(screen.getByText("Informe o nome.")).toBeInTheDocument();
    });
    expect(captured).toBeNull();
  });

  it("submit sem grupo muscular → erro de validação exibido, POST não chamado", async () => {
    server.use(
      http.get("*/admin/grupos-musculares", () => HttpResponse.json([])),
    );
    const user = userEvent.setup();
    const { default: Page } = await import("@/app/(admin)/admin/exercicios/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await user.click(await screen.findByRole("button", { name: /novo exercício/i }));
    const dialog = await screen.findByRole("dialog");

    await user.type(within(dialog).getByRole("textbox", { name: /nome/i }), "Supino");
    await user.click(within(dialog).getByRole("button", { name: /^adicionar$/i }));

    await waitFor(() => {
      expect(screen.getByText("Selecione o grupo muscular.")).toBeInTheDocument();
    });
    expect(captured).toBeNull();
  });

  it("submit válido → POST chamado com nome e grupoMuscularId corretos", async () => {
    const user = userEvent.setup();
    const { default: Page } = await import("@/app/(admin)/admin/exercicios/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await user.click(await screen.findByRole("button", { name: /novo exercício/i }));
    const dialog = await screen.findByRole("dialog");

    await user.type(within(dialog).getByRole("textbox", { name: /nome/i }), "Supino");

    const grupoSelect = within(dialog).getByRole("combobox", { name: /grupo muscular/i });
    fireEvent.mouseDown(grupoSelect);
    fireEvent.click(await screen.findByRole("option", { name: "Peito" }));

    await user.click(within(dialog).getByRole("button", { name: /^adicionar$/i }));

    await waitFor(() => {
      expect(captured).toMatchObject({ nome: "Supino", grupoMuscularId: "gm-1" });
    });
  });
});
