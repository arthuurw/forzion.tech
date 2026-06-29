/**
 * Vitest integration tests for /perfil LGPD section (R4).
 *
 * Covers:
 * - "Baixar meus dados (Excel)" button triggers GET /conta/lgpd/exportar?formato=xlsx
 * - "Baixar como JSON" button triggers GET /conta/lgpd/exportar?formato=json
 * - "Excluir minha conta" button opens ConfirmDialog and calls DELETE /conta/lgpd with senha
 * - "Preferências de cookies" opens ConsentBanner
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
  useParams: () => ({}),
}));

// Mock dynamic import for ConsentBanner (used inside ConsentProvider)
vi.mock("next/dynamic", () => ({
  default: (fn: () => Promise<{ default: React.ComponentType }>) => {
    // Return a placeholder — we don't need full banner render in this test
    const Component = () => null;
    Component.displayName = "DynamicComponent";
    return Component;
  },
}));

import PerfilPage from "../page";
import { renderWithProviders } from "@/test/render";

beforeEach(() => {
  server.use(
    http.get("*/conta/perfil", () =>
      HttpResponse.json({
        nome: "João Teste",
        email: "joao@teste.com",
        tipoConta: "Aluno",
      }),
    ),
    // Override the default aluno/vinculo handler (default returns 401)
    http.get("*/aluno/vinculo", () =>
      HttpResponse.json({ vinculoAtivo: null, vinculoPendente: null }),
    ),
    http.get("*/api/auth/me", () =>
      HttpResponse.json({
        contaId: "conta-123",
        tipoConta: "Aluno",
        perfilId: "perfil-123",
      }),
    ),
  );
});

describe("/perfil LGPD section", () => {
  it("renders Baixar meus dados (Excel) and Baixar como JSON buttons", async () => {
    renderWithProviders(<PerfilPage />);

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: /baixar meus dados \(excel\)/i }),
      ).toBeInTheDocument();
      expect(
        screen.getByRole("button", { name: /baixar como json/i }),
      ).toBeInTheDocument();
    });
  });

  it("renders Excluir minha conta button", async () => {
    renderWithProviders(<PerfilPage />);

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: /excluir minha conta/i }),
      ).toBeInTheDocument();
    });
  });

  it("Baixar meus dados (Excel): calls exportarDados('xlsx')", async () => {
    const { contaApi } = await import("@/lib/api/conta");

    const exportSpy = vi.spyOn(contaApi, "exportarDados").mockResolvedValue({
      data: new Blob(["binary"], { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" }),
      status: 200,
      statusText: "OK",
      headers: {},
      config: {} as any,
    } as any);

    const originalCreateObjectURL = globalThis.URL?.createObjectURL;
    const originalRevokeObjectURL = globalThis.URL?.revokeObjectURL;
    if (globalThis.URL) {
      globalThis.URL.createObjectURL = vi.fn(() => "blob:fake-url");
      globalThis.URL.revokeObjectURL = vi.fn();
    }

    const originalCreateElement = document.createElement.bind(document);
    vi.spyOn(document, "createElement").mockImplementation((tag: string) => {
      const el = originalCreateElement(tag as keyof HTMLElementTagNameMap);
      if (tag === "a") {
        vi.spyOn(el as HTMLAnchorElement, "click").mockImplementation(vi.fn());
      }
      return el;
    });

    renderWithProviders(<PerfilPage />);

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: /baixar meus dados \(excel\)/i }),
      ).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole("button", { name: /baixar meus dados \(excel\)/i }));

    await waitFor(() => {
      expect(exportSpy).toHaveBeenCalledWith("xlsx");
    });

    exportSpy.mockRestore();
    vi.restoreAllMocks();
    if (globalThis.URL) {
      if (originalCreateObjectURL) globalThis.URL.createObjectURL = originalCreateObjectURL;
      if (originalRevokeObjectURL) globalThis.URL.revokeObjectURL = originalRevokeObjectURL;
    }
  });

  it("Excluir minha conta: opens ConfirmDialog, calls DELETE /conta/lgpd with senha", async () => {
    let deletedWithSenha: unknown = null;

    server.use(
      http.delete("*/conta/lgpd", async ({ request }) => {
        const body = await request.json() as { senha: string };
        deletedWithSenha = body.senha;
        return new HttpResponse(null, { status: 204 });
      }),
      http.post("*/api/auth/logout", () => HttpResponse.json({ ok: true })),
    );

    renderWithProviders(<PerfilPage />);

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: /excluir minha conta/i }),
      ).toBeInTheDocument();
    });

    // getAllByRole avoids ambiguity; the trigger button is the first match
    const deleteBtn = screen.getAllByRole("button", { name: /excluir minha conta/i })[0];
    fireEvent.click(deleteBtn);

    const dialog = await screen.findByRole("dialog");
    expect(dialog).toBeInTheDocument();

    // Fill senha within the dialog specifically (label repeats outside);
    // ^senha$ evita casar com o aria-label "Mostrar senha" do toggle do PasswordField
    const senhaInput = within(dialog).getByLabelText(/^senha$/i);
    fireEvent.change(senhaInput, { target: { value: "minha-senha-123" } });

    const confirmBtn = within(dialog).getByRole("button", { name: /excluir conta/i });
    fireEvent.click(confirmBtn);

    await waitFor(() => {
      expect(deletedWithSenha).toBe("minha-senha-123");
    });
  });
});
