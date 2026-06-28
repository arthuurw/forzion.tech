import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import PerfilPage from "../page";
import { renderWithProviders } from "@/test/render";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
  useParams: () => ({}),
}));

vi.mock("next/dynamic", () => ({
  default: () => {
    const Component = () => null;
    Component.displayName = "DynamicComponent";
    return Component;
  },
}));

beforeEach(() => {
  server.use(
    http.get("*/conta/perfil", () =>
      HttpResponse.json({ nome: "João Teste", email: "joao@teste.com", tipoConta: "Aluno" }),
    ),
    http.get("*/aluno/vinculo", () =>
      HttpResponse.json({ vinculoAtivo: null, vinculoPendente: null }),
    ),
    http.get("*/api/auth/me", () =>
      HttpResponse.json({ contaId: "c1", tipoConta: "Aluno", perfilId: "p1" }),
    ),
  );
});

describe("/perfil — a11y headings (FPAD-04)", () => {
  it("título da página é um h1 único", async () => {
    renderWithProviders(<PerfilPage />);
    const h1 = await screen.findByRole("heading", { level: 1 });
    expect(h1).toHaveTextContent("Meu Perfil");
    expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
  });

  it("títulos de seção são h2 — hierarquia desce sem salto de nível", async () => {
    renderWithProviders(<PerfilPage />);
    await screen.findByText("joao@teste.com");

    const niveis = screen
      .getAllByRole("heading")
      .map((h) => Number(h.tagName[1]));
    expect(Math.max(...niveis)).toBe(2);
    expect(niveis.filter((n) => n === 1)).toHaveLength(1);

    const h2Textos = screen.getAllByRole("heading", { level: 2 }).map((h) => h.textContent);
    expect(h2Textos).toContain("Dados da conta");
    expect(h2Textos).toContain("Alterar senha");
  });
});
