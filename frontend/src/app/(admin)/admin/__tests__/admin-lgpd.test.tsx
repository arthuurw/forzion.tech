import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { adminApi } from "@/lib/api/admin";
import { renderWithProviders } from "@/test/render";

// Must be at top-level for vitest hoisting
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn(), back: vi.fn() }),
  useParams: () => ({ treinadorId: "treinador-1", alunoId: "aluno-1" }),
  usePathname: () => "/admin",
  useSearchParams: () => new URLSearchParams(),
}));

describe("adminApi LGPD methods", () => {
  it("exportarDadosConta calls GET /admin/contas/{contaId}/lgpd/exportar", async () => {
    let capturedContaId = "";

    server.use(
      http.get("*/admin/contas/:contaId/lgpd/exportar", ({ params }) => {
        capturedContaId = params.contaId as string;
        return new HttpResponse(new Blob(['{"dados":"ok"}'], { type: "application/json" }), {
          headers: { "Content-Type": "application/json" },
        });
      }),
    );

    await adminApi.exportarDadosConta("conta-abc-123");
    expect(capturedContaId).toBe("conta-abc-123");
  });

  it("anonimizarConta calls DELETE /admin/contas/{contaId}/lgpd", async () => {
    let capturedContaId = "";

    server.use(
      http.delete("*/admin/contas/:contaId/lgpd", ({ params }) => {
        capturedContaId = params.contaId as string;
        return new HttpResponse(null, { status: 204 });
      }),
    );

    await adminApi.anonimizarConta("conta-xyz-456");
    expect(capturedContaId).toBe("conta-xyz-456");
  });
});

describe("admin pages LGPD integration", () => {
  it("treinador detail: LGPD tab renders export and anonimizar buttons", async () => {
    server.use(
      http.get("*/admin/treinadores/treinador-1", () =>
        HttpResponse.json({
          treinadorId: "treinador-1",
          contaId: "conta-treinador-1",
          nome: "Treinador Teste",
          email: "treinador@teste.com",
          status: "Ativo",
          createdAt: new Date().toISOString(),
          updatedAt: null,
          observacao: null,
        }),
      ),
      http.get("*/admin/treinadores/treinador-1/alunos", () =>
        HttpResponse.json({ items: [], total: 0, pagina: 1, tamanhoPagina: 20 }),
      ),
    );

    const { default: DetalheTreinadorAdminPage } = await import(
      "../treinadores/[treinadorId]/page"
    );

    renderWithProviders(<DetalheTreinadorAdminPage />, { skipAuth: true });

    await waitFor(() => {
      expect(screen.getByText("LGPD")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText("LGPD"));

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: /exportar dados.*lgpd/i }),
      ).toBeInTheDocument();
      expect(
        screen.getByRole("button", { name: /anonimizar conta.*lgpd/i }),
      ).toBeInTheDocument();
    });
  });

  it("aluno detail: LGPD tab renders export and anonimizar buttons", async () => {
    server.use(
      http.get("*/admin/alunos/aluno-1", () =>
        HttpResponse.json({
          alunoId: "aluno-1",
          contaId: "conta-aluno-1",
          nome: "Aluno Teste",
          email: "aluno@teste.com",
          status: "Ativo",
          createdAt: new Date().toISOString(),
          updatedAt: null,
          telefone: null,
          finalidade: null,
          nivelCondicionamento: null,
          diasDisponiveis: null,
          tempoDisponivelMinutos: null,
          focoTreino: null,
          limitacoesFisicas: null,
          doencas: null,
          observacoesAdicionais: null,
        }),
      ),
      http.get("*/admin/alunos/aluno-1/vinculo", () =>
        HttpResponse.json({ vinculoAtivo: null, vinculoPendente: null }),
      ),
      http.get("*/admin/alunos/aluno-1/fichas", () =>
        HttpResponse.json({ items: [], total: 0, pagina: 1, tamanhoPagina: 20 }),
      ),
    );

    const { default: DetalheAlunoAdminPage } = await import(
      "../alunos/[alunoId]/page"
    );

    renderWithProviders(<DetalheAlunoAdminPage />, { skipAuth: true });

    await waitFor(() => {
      expect(screen.getByText("LGPD")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText("LGPD"));

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: /exportar dados.*lgpd/i }),
      ).toBeInTheDocument();
      expect(
        screen.getByRole("button", { name: /anonimizar conta.*lgpd/i }),
      ).toBeInTheDocument();
    });
  });
});
