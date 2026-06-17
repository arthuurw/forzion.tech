import { describe, it, expect, beforeEach, vi } from "vitest";
import { screen, fireEvent, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";

const { pushMock } = vi.hoisted(() => ({ pushMock: vi.fn() }));
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: pushMock, replace: vi.fn() }),
  useParams: () => ({}),
}));

import LoginPage from "../page";
import { renderWithProviders } from "@/test/render";

const sessao = { tipoConta: "Aluno", contaId: "c1", perfilId: "p1", nome: "Maria" };

function pendingLogin() {
  return http.post("*/api/auth", () =>
    HttpResponse.json({ mfaRequerido: true, mfaPendingExpiraEm: "2026-06-17T13:00:00Z" }),
  );
}

async function preencherLogin() {
  fireEvent.change(await screen.findByLabelText(/e-mail/i), { target: { value: "u@u.com" } });
  fireEvent.change(screen.getByLabelText(/^senha/i), { target: { value: "senha123" } });
  fireEvent.click(screen.getByRole("button", { name: /entrar/i }));
}

beforeEach(() => {
  pushMock.mockClear();
});

describe("/login — segunda etapa MFA", () => {
  it("login com MFA pendente renderiza o campo de código", async () => {
    server.use(pendingLogin());

    renderWithProviders(<LoginPage />);
    await preencherLogin();

    expect(await screen.findByLabelText("Código de verificação")).toBeInTheDocument();
    expect(screen.getByText(/aplicativo autenticador/i)).toBeInTheDocument();
  });

  it("verifica o código e redireciona para a home da conta", async () => {
    let body: { codigo?: string; lembrarDispositivo?: boolean } = {};
    server.use(
      pendingLogin(),
      http.post("*/api/auth/mfa/verificar", async ({ request }) => {
        body = (await request.json()) as typeof body;
        return HttpResponse.json(sessao);
      }),
    );

    renderWithProviders(<LoginPage />);
    await preencherLogin();

    fireEvent.click(await screen.findByLabelText(/lembrar este dispositivo/i));
    fireEvent.change(await screen.findByLabelText("Código de verificação"), { target: { value: "123456" } });
    fireEvent.click(screen.getByRole("button", { name: /verificar/i }));

    await waitFor(() => expect(pushMock).toHaveBeenCalledWith("/aluno"));
    expect(body.codigo).toBe("123456");
    expect(body.lembrarDispositivo).toBe(true);
  });

  it("fallback por e-mail: envia o código e troca a instrução", async () => {
    let enviado = false;
    server.use(
      pendingLogin(),
      http.post("*/api/auth/mfa/email/enviar", () => {
        enviado = true;
        return HttpResponse.json({ ok: true });
      }),
    );

    renderWithProviders(<LoginPage />);
    await preencherLogin();

    fireEvent.click(await screen.findByRole("button", { name: /usar código por e-mail/i }));

    expect(await screen.findByText(/enviamos para o seu e-mail/i)).toBeInTheDocument();
    await waitFor(() => expect(enviado).toBe(true));
  });

  it("código inválido exibe o erro mapeado do backend", async () => {
    server.use(
      pendingLogin(),
      http.post("*/api/auth/mfa/verificar", () =>
        HttpResponse.json({ detail: "Código inválido." }, { status: 422 }),
      ),
    );

    renderWithProviders(<LoginPage />);
    await preencherLogin();

    fireEvent.change(await screen.findByLabelText("Código de verificação"), { target: { value: "000000" } });
    fireEvent.click(screen.getByRole("button", { name: /verificar/i }));

    expect(await screen.findByText("Código inválido.")).toBeInTheDocument();
    expect(pushMock).not.toHaveBeenCalled();
  });
});
