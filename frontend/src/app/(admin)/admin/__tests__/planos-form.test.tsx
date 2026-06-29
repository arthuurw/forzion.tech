import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { renderWithProviders } from "@/test/render";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn() })),
  useParams: vi.fn(() => ({})),
}));

const PLANO_EXISTENTE = {
  planoId: "pl1",
  nome: "Basic",
  tier: "Basic",
  maxAlunos: 10,
  preco: 49.9,
  descricao: null,
  isAtivo: true,
};

describe("PlanosAdminPage — form de criação RHF+Zod", () => {
  let captured: unknown = null;

  beforeEach(() => {
    captured = null;
    server.use(
      http.get("*/admin/planos", () => HttpResponse.json([PLANO_EXISTENTE])),
      http.post("*/admin/planos", async ({ request }) => {
        captured = await request.json();
        return HttpResponse.json({ ...PLANO_EXISTENTE, planoId: "pl2" }, { status: 201 });
      }),
    );
  });

  it("inválido: nome vazio → erro Zod exibido, POST não chamado", async () => {
    const user = userEvent.setup();
    const { default: Page } = await import("@/app/(admin)/admin/planos/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await user.click(await screen.findByRole("button", { name: /novo plano/i }));
    await screen.findByRole("dialog");

    await user.click(screen.getByRole("button", { name: /^criar$/i }));

    await waitFor(() => {
      expect(screen.getByText("Informe o nome.")).toBeInTheDocument();
    });
    expect(captured).toBeNull();
  });

  it("válido: campos preenchidos → POST com types numéricos corretos", async () => {
    const user = userEvent.setup();
    const { default: Page } = await import("@/app/(admin)/admin/planos/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await user.click(await screen.findByRole("button", { name: /novo plano/i }));
    await screen.findByRole("dialog");

    await user.type(screen.getByLabelText(/nome/i), "Plano Teste");

    const maxAlunosInput = screen.getByLabelText(/máximo de alunos/i);
    await user.clear(maxAlunosInput);
    await user.type(maxAlunosInput, "25");

    const precoInput = screen.getByLabelText(/preço/i);
    await user.clear(precoInput);
    await user.type(precoInput, "99");

    await user.click(screen.getByRole("button", { name: /^criar$/i }));

    await waitFor(() => {
      expect(captured).not.toBeNull();
    });

    expect(captured).toMatchObject({
      nome: "Plano Teste",
      tier: "Basic",
      maxAlunos: 25,
      preco: 99,
    });
    expect(typeof (captured as Record<string, unknown>).maxAlunos).toBe("number");
    expect(typeof (captured as Record<string, unknown>).preco).toBe("number");
  });

  it("boundary: maxAlunos zero → erro Zod exibido, POST não chamado", async () => {
    const user = userEvent.setup();
    const { default: Page } = await import("@/app/(admin)/admin/planos/page");
    renderWithProviders(<Page />, { skipAuth: true });

    await user.click(await screen.findByRole("button", { name: /novo plano/i }));
    await screen.findByRole("dialog");

    await user.type(screen.getByLabelText(/nome/i), "Teste");

    const maxAlunosInput = screen.getByLabelText(/máximo de alunos/i);
    await user.clear(maxAlunosInput);
    await user.type(maxAlunosInput, "0");

    await user.click(screen.getByRole("button", { name: /^criar$/i }));

    await waitFor(() => {
      expect(screen.getByText("Mínimo 1 aluno.")).toBeInTheDocument();
    });
    expect(captured).toBeNull();
  });
});
