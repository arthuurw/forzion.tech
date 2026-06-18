import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn() })),
}));

vi.mock("@/hooks/usePaginatedList", () => ({
  usePaginatedList: vi.fn(() => ({
    items: [], total: 0, page: 0, pageSize: 20, loading: false,
    error: "", success: "",
    setPage: vi.fn(), setPageSize: vi.fn(), setError: vi.fn(), setSuccess: vi.fn(), reload: vi.fn(),
  })),
}));

describe("ExerciciosAdminPage — orientação (como executar + vídeo)", () => {
  beforeEach(() => {
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
});
