/**
 * Login — bloqueio de treinador não-ativo.
 * Backend retorna 403 + code; a página deve exibir o aviso correspondente
 * (aguardando aprovação / conta inativa) em vez de logar.
 */
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import LoginPage from "../login/page";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), replace: vi.fn(), back: vi.fn() })),
}));

vi.mock("@/lib/auth/context", () => ({
  useAuth: () => ({ login: vi.fn(), user: null, isLoading: false }),
  homeRouteFor: () => "/",
}));

async function submeterLogin() {
  fireEvent.change(screen.getByLabelText(/E-mail/, { selector: "input" }), { target: { value: "trainer@test.com" } });
  fireEvent.change(screen.getByLabelText(/Senha/, { selector: "input" }), { target: { value: "Senha123" } });
  fireEvent.click(screen.getByRole("button", { name: /entrar/i }));
}

describe("Login — bloqueio de treinador", () => {
  it("treinador aguardando aprovação → exibe aviso de análise", async () => {
    server.use(
      http.post("*/api/auth", () =>
        HttpResponse.json({ title: "Aguardando aprovação", code: "TREINADOR_AGUARDANDO_APROVACAO" }, { status: 403 }),
      ),
    );

    render(<LoginPage />);
    await submeterLogin();

    expect(await screen.findByText(/cadastro em análise/i)).toBeInTheDocument();
    expect(screen.getByText(/aguarde a aprovação do administrador/i)).toBeInTheDocument();
  });

  it("treinador inativo → exibe aviso de conta inativa", async () => {
    server.use(
      http.post("*/api/auth", () =>
        HttpResponse.json({ title: "Conta inativa", code: "TREINADOR_INATIVO" }, { status: 403 }),
      ),
    );

    render(<LoginPage />);
    await submeterLogin();

    expect(await screen.findByText(/conta inativa/i)).toBeInTheDocument();
    expect(screen.getByText(/entre em contato com o suporte/i)).toBeInTheDocument();
  });
});
