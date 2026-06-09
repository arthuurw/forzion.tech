import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn() })),
}));

vi.mock("@/hooks/usePaginatedList", () => ({
  usePaginatedList: vi.fn(() => ({
    items: [], total: 0, page: 0, pageSize: 20, loading: false,
    error: "Erro ao carregar fichas.", success: "",
    setPage: vi.fn(), setPageSize: vi.fn(), setError: vi.fn(),
    setSuccess: vi.fn(), reload: vi.fn(),
  })),
}));

describe("FichasAlunoPage — erro", () => {
  it("erro do hook → exibe AlertBanner", async () => {
    const { default: Page } = await import("@/app/(aluno)/aluno/fichas/page");
    render(<Page />);
    expect(screen.getByText("Erro ao carregar fichas.")).toBeInTheDocument();
  });
});
