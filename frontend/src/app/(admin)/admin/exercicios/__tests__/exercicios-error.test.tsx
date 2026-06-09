import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn() })),
}));

const setError = vi.fn();

vi.mock("@/hooks/usePaginatedList", () => ({
  usePaginatedList: vi.fn(() => ({
    items: [], total: 0, page: 0, pageSize: 20, loading: false,
    error: "", success: "",
    setPage: vi.fn(), setPageSize: vi.fn(), setError, setSuccess: vi.fn(), reload: vi.fn(),
  })),
}));

describe("ExerciciosAdminPage — grupos musculares indisponíveis", () => {
  beforeEach(() => {
    setError.mockClear();
    server.use(
      http.get("*/admin/exercicios", () =>
        HttpResponse.json({ items: [], total: 0, pagina: 1, tamanhoPagina: 20 }),
      ),
    );
  });

  // setError vem do hook (mockado). Provar que é chamado basta: a falha de
  // grupos deixa de ser muda e alimenta o AlertBanner real da página.
  it("listGruposMusculares falha → chama setError do hook", async () => {
    // resposta sem detail/title/message → page recorre ao fallback honesto
    server.use(
      http.get("*/admin/grupos-musculares", () => new HttpResponse(null, { status: 500 })),
    );
    const { default: Page } = await import("@/app/(admin)/admin/exercicios/page");
    render(<Page />);
    await vi.waitFor(() => {
      expect(setError).toHaveBeenCalledWith(
        "Não foi possível carregar os grupos musculares. O cadastro de exercícios fica indisponível.",
      );
    });
  });
});
