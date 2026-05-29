/**
 * Tests for Elite-tier "Em breve" behaviour in admin pages:
 *  - PlanosAdminPage: Elite option is disabled in tier dropdowns (create + edit).
 *  - TreinadoresAdminPage: Elite-tier plan is excluded from the plan-assignment autocomplete.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, fireEvent, within } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { buildPlano } from "@/test/factories/plano";
import type { TreinadorResponse } from "@/types";

// ─── Global mocks ────────────────────────────────────────────────────────────

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

// ─── PlanosAdminPage ─────────────────────────────────────────────────────────

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

    // Open create dialog
    fireEvent.click(screen.getByRole("button", { name: /novo plano/i }));

    // The dialog should be visible — find the Tier select
    await waitFor(() => {
      expect(screen.getByRole("dialog")).toBeInTheDocument();
    });

    // MUI TextField select renders a <div role="combobox"> or the select button.
    // Open the Tier dropdown by clicking the select's button element.
    // The dialog contains a select labeled "Tier"
    const dialog = screen.getByRole("dialog");
    // The select input for Tier — MUI renders it as a div with role="combobox" or button
    // Find the select button within the dialog
    const tierInput = within(dialog).getByRole("combobox", { name: /tier/i });
    fireEvent.mouseDown(tierInput);

    // Now the listbox should be in the DOM
    await waitFor(() => {
      const listbox = screen.getByRole("listbox");
      const eliteOption = within(listbox).getByText("Elite (em breve)");
      expect(eliteOption).toBeInTheDocument();
      // MUI MenuItem disabled renders as aria-disabled="true" on the li
      const li = eliteOption.closest("li");
      expect(li).toHaveAttribute("aria-disabled", "true");
    });
  });

  it("dialog Editar — opção 'Elite (em breve)' está disabled no select de Tier", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/planos/page");
    render(<Page />);

    // Wait for plan card to render
    await waitFor(() => {
      expect(screen.getByText("Plano Basic")).toBeInTheDocument();
    });

    // Click the edit button (aria-label="Editar")
    fireEvent.click(screen.getByRole("button", { name: "Editar" }));

    // Edit dialog should open
    await waitFor(() => {
      expect(screen.getByRole("dialog")).toBeInTheDocument();
    });

    const dialog = screen.getByRole("dialog");
    const tierInput = within(dialog).getByRole("combobox", { name: /tier/i });
    fireEvent.mouseDown(tierInput);

    await waitFor(() => {
      const listbox = screen.getByRole("listbox");
      const eliteOption = within(listbox).getByText("Elite (em breve)");
      expect(eliteOption).toBeInTheDocument();
      const li = eliteOption.closest("li");
      expect(li).toHaveAttribute("aria-disabled", "true");
    });
  });
});

// ─── TreinadoresAdminPage — Elite excluído do autocomplete ───────────────────

describe("TreinadoresAdminPage — Elite excluído do autocomplete de plano", () => {
  const planoBasic = buildPlano({ tier: "Basic", nome: "Plano Basic" });
  const planoElite = buildPlano({ tier: "Elite", nome: "Plano Elite" });

  const mockTreinador: TreinadorResponse = {
    treinadorId: "t-1",
    nome: "Treinador Teste",
    contaId: "c-1",
    status: "Ativo",
    planoPlataformaId: null,
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

  it("dialog de atribuição de plano não exibe plano Elite como opção", async () => {
    const { default: Page } = await import("@/app/(admin)/admin/treinadores/page");
    render(<Page />);

    // Wait for the treinador row to render
    await waitFor(() => {
      expect(screen.getByText("Treinador Teste")).toBeInTheDocument();
    });

    // Click the "Atribuir plano" button (CardMembershipIcon)
    const membershipBtn = screen.getAllByRole("button").find((b) =>
      b.querySelector("[data-testid='CardMembershipIcon']"),
    );
    expect(membershipBtn).toBeDefined();
    fireEvent.click(membershipBtn!);

    // Wait for the plan assignment dialog to open and plans to load from MSW
    await waitFor(() => {
      expect(screen.getByRole("dialog")).toBeInTheDocument();
    });

    // Open the Autocomplete dropdown
    const combobox = screen.getByRole("combobox");
    fireEvent.mouseDown(combobox);
    fireEvent.change(combobox, { target: { value: "" } });

    // Wait for options to appear
    await waitFor(() => {
      const listbox = screen.getByRole("listbox");
      // Basic plan should be present
      expect(within(listbox).getByText(/Plano Basic/i)).toBeInTheDocument();
      // Elite plan must NOT be in the listbox
      expect(within(listbox).queryByText(/Plano Elite/i)).not.toBeInTheDocument();
    });
  });
});
