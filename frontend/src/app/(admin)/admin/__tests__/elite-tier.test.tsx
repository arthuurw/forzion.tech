import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, fireEvent, within } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { renderWithProviders } from "@/test/render";
import { buildPlano } from "@/test/factories/plano";
import type { TreinadorResponse } from "@/types";

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

import { usePaginatedList } from "@/hooks/usePaginatedList";
const mockUsePaginatedList = vi.mocked(usePaginatedList);

describe("PlanosAdminPage — Elite desabilitado no dropdown de tier", () => {
  const planoBasic = buildPlano({ tier: "Basic", nome: "Plano Basic" });

  beforeEach(() => {
    server.use(
      http.get("*/admin/planos", () => HttpResponse.json([planoBasic])),
    );
  });

  it("dialog Criar — opção 'Elite (em breve)' está disabled no select de Tier", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/planos/page");
    render(<Page />);

    fireEvent.click(screen.getByRole("button", { name: /novo plano/i }));

    await waitFor(() => {
      expect(screen.getByRole("dialog")).toBeInTheDocument();
    });

    // MUI TextField select renders a <div role="combobox">; open the Tier dropdown via mouseDown.
    const dialog = screen.getByRole("dialog");
    const tierInput = within(dialog).getByRole("combobox", { name: /tier/i });
    fireEvent.mouseDown(tierInput);

    await waitFor(() => {
      const listbox = screen.getByRole("listbox");
      const eliteOption = within(listbox).getByRole("option", { name: "Elite (em breve)" });
      expect(eliteOption).toHaveAttribute("aria-disabled", "true");
    });
  });

  it("dialog Editar — opção 'Elite (em breve)' está disabled no select de Tier", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/planos/page");
    render(<Page />);

    await waitFor(() => {
      expect(screen.getByText("Plano Basic")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole("button", { name: "Editar" }));

    await waitFor(() => {
      expect(screen.getByRole("dialog")).toBeInTheDocument();
    });

    const dialog = screen.getByRole("dialog");
    const tierInput = within(dialog).getByRole("combobox", { name: /tier/i });
    fireEvent.mouseDown(tierInput);

    await waitFor(() => {
      const listbox = screen.getByRole("listbox");
      const eliteOption = within(listbox).getByRole("option", { name: "Elite (em breve)" });
      expect(eliteOption).toHaveAttribute("aria-disabled", "true");
    });
  });
});

describe("TreinadoresAdminPage — Elite excluído do autocomplete de plano", () => {
  const planoBasic = buildPlano({ tier: "Basic", nome: "Plano Basic" });
  const planoElite = buildPlano({ tier: "Elite", nome: "Plano Elite" });

  const mockTreinador: TreinadorResponse = {
    treinadorId: "t-1",
    nome: "Treinador Teste",
    contaId: "c-1",
    status: "Ativo",
    planoPlataformaId: null,
    planoCortesiaId: null,
    createdAt: "2025-01-01T00:00:00Z",
  };

  beforeEach(() => {
    server.use(
      http.get("*/admin/planos", () => HttpResponse.json([planoBasic, planoElite])),
    );
    mockUsePaginatedList.mockReturnValue({
      items: [mockTreinador],
      total: 1,
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
    });
  });

  it("dialog de cortesia não exibe plano Elite como opção", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/treinadores/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await waitFor(() => {
      expect(screen.getByText("Treinador Teste")).toBeInTheDocument();
    });

    const membershipBtn = screen.getByRole("button", { name: "Conceder cortesia" });
    expect(membershipBtn).toBeInTheDocument();
    fireEvent.click(membershipBtn);

    await waitFor(() => {
      expect(screen.getByRole("dialog")).toBeInTheDocument();
    });

    const combobox = screen.getByRole("combobox");
    fireEvent.mouseDown(combobox);
    fireEvent.change(combobox, { target: { value: "" } });

    await waitFor(() => {
      const listbox = screen.getByRole("listbox");
      expect(within(listbox).getByText(/Plano Basic/i)).toBeInTheDocument();
      expect(within(listbox).queryByText(/Plano Elite/i)).not.toBeInTheDocument();
    });
  });
});
