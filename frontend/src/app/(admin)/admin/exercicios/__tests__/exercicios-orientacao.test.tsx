import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { usePaginatedList } from "@/hooks/usePaginatedList";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn() })),
}));

const listState = {
  items: [], total: 0, page: 0, pageSize: 20, loading: false,
  error: "", success: "",
  setPage: vi.fn(), setPageSize: vi.fn(), setError: vi.fn(), setSuccess: vi.fn(), reload: vi.fn(),
};

vi.mock("@/hooks/usePaginatedList", () => ({
  usePaginatedList: vi.fn(() => listState),
}));

describe("ExerciciosAdminPage — orientação (como executar + vídeo)", () => {
  beforeEach(() => {
    vi.mocked(usePaginatedList).mockReturnValue(listState as ReturnType<typeof usePaginatedList<unknown>>);
    server.use(
      http.get("*/admin/grupos-musculares", () =>
        HttpResponse.json([{ id: "g-1", nome: "Peito" }]),
      ),
    );
  });

  it("link inválido desabilita submit e mostra helper", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/exercicios/page");
    render(<Page />);

    await userEvent.click(screen.getByRole("button", { name: /Novo exercício/i }));
    const dialog = within(screen.getByRole("dialog"));
    await userEvent.type(dialog.getByLabelText(/Nome/i), "Supino");
    await userEvent.type(dialog.getByLabelText(/Link do vídeo/i), "não é link");

    expect(dialog.getByText(/link ou ID de vídeo do YouTube válido/i)).toBeInTheDocument();
    expect(dialog.getByRole("button", { name: "Adicionar" })).toBeDisabled();
  });

  it("envia comoExecutar e videoUrl no create", async () => {
    let body: Record<string, unknown> | null = null;
    server.use(
      http.post("*/admin/exercicios", async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({ exercicioId: "x-1", nome: "Supino" }, { status: 201 });
      }),
    );

    const { default: Page } = await import("@/app/(admin)/admin/exercicios/page");
    render(<Page />);

    await userEvent.click(screen.getByRole("button", { name: /Novo exercício/i }));
    const dialog = within(screen.getByRole("dialog"));
    await userEvent.type(dialog.getByLabelText(/Nome/i), "Supino");
    await userEvent.type(dialog.getByLabelText(/Como executar/i), "Desça controlado.");
    await userEvent.type(dialog.getByLabelText(/Link do vídeo/i), "https://youtu.be/dQw4w9WgXcQ");
    await userEvent.click(dialog.getByRole("button", { name: "Adicionar" }));

    await waitFor(() => expect(body).not.toBeNull());
    expect(body!.comoExecutar).toBe("Desça controlado.");
    expect(body!.videoUrl).toBe("https://youtu.be/dQw4w9WgXcQ");
  });

  it("limpar campos no editar envia string vazia (backend limpa)", async () => {
    vi.mocked(usePaginatedList).mockReturnValue({
      ...listState,
      items: [{
        exercicioId: "x-1", nome: "Supino", descricao: null,
        comoExecutar: "Desça controlado.", videoId: "dQw4w9WgXcQ",
        grupoMuscularId: "g-1", grupoMuscular: "Peito",
        treinadorId: null, isGlobal: true,
      }],
      total: 1,
    } as ReturnType<typeof usePaginatedList<unknown>>);

    let body: Record<string, unknown> | null = null;
    server.use(
      http.patch("*/admin/exercicios/x-1", async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({ exercicioId: "x-1", nome: "Supino" });
      }),
    );

    const { default: Page } = await import("@/app/(admin)/admin/exercicios/page");
    render(<Page />);

    await userEvent.click(screen.getByRole("button", { name: "Editar exercício" }));
    const dialog = within(screen.getByRole("dialog"));
    await userEvent.clear(dialog.getByLabelText(/Como executar/i));
    await userEvent.clear(dialog.getByLabelText(/Link do vídeo/i));
    await userEvent.click(dialog.getByRole("button", { name: "Salvar" }));

    await waitFor(() => expect(body).not.toBeNull());
    expect(body!.comoExecutar).toBe("");
    expect(body!.videoUrl).toBe("");
  });
});
