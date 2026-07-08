import { describe, it, expect, vi } from "vitest";
import { screen, fireEvent, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  useParams: () => ({}),
  useSearchParams: () => new URLSearchParams("token=" + "a".repeat(64)),
}));

import ResetPasswordPage from "../page";
import { renderWithProviders } from "@/test/render";

async function preencherSenha(senha = "SenhaForte123", confirma = senha) {
  fireEvent.change(await screen.findByLabelText(/^Nova senha/), { target: { value: senha } });
  fireEvent.change(screen.getByLabelText(/^Confirmar nova senha/), { target: { value: confirma } });
  fireEvent.click(screen.getByRole("button", { name: /redefinir senha/i }));
}

describe("/reset-password — MFA e mapeamento de erros (FEAUTH-01/02/03)", () => {
  it("conta sem MFA conclui na 1ª submissão sem campo TOTP (AC-1.4)", async () => {
    let body: unknown = null;
    server.use(
      http.post("*/api/auth/reset-password", async ({ request }) => {
        body = (await request.json()) as typeof body;
        return HttpResponse.json({}, { status: 200 });
      }),
    );

    renderWithProviders(<ResetPasswordPage />);
    expect(screen.queryByLabelText("Código de verificação")).not.toBeInTheDocument();
    await preencherSenha();

    expect(await screen.findByText("Senha redefinida!")).toBeInTheDocument();
    expect(body).toEqual({ token: "a".repeat(64), novaSenha: "SenhaForte123" });
  });

  it("422 mfa.codigo_invalido revela o campo TOTP e preserva a senha (AC-1.2, EC-1, EC-3)", async () => {
    server.use(
      http.post("*/api/auth/reset-password", () =>
        HttpResponse.json({ code: "mfa.codigo_invalido", detail: "O código informado é inválido." }, { status: 422 }),
      ),
    );

    renderWithProviders(<ResetPasswordPage />);
    await preencherSenha("SenhaForte123");

    expect(await screen.findByLabelText("Código de verificação")).toBeInTheDocument();
    expect(screen.getAllByText(/verificação em duas etapas/i).length).toBeGreaterThan(0);
    expect(screen.getByLabelText(/^Nova senha/)).toHaveValue("SenhaForte123");
    expect(screen.getByLabelText(/^Confirmar nova senha/)).toHaveValue("SenhaForte123");
  });

  it("reenvio com codigoTotp válido conclui com sucesso (AC-1.1, AC-1.3)", async () => {
    let body: { token?: string; novaSenha?: string; codigoTotp?: string } = {};
    let primeiraChamada = true;
    server.use(
      http.post("*/api/auth/reset-password", async ({ request }) => {
        if (primeiraChamada) {
          primeiraChamada = false;
          return HttpResponse.json({ code: "mfa.codigo_invalido" }, { status: 422 });
        }
        body = (await request.json()) as typeof body;
        return HttpResponse.json({}, { status: 200 });
      }),
    );

    renderWithProviders(<ResetPasswordPage />);
    await preencherSenha("SenhaForte123");
    await screen.findByLabelText("Código de verificação");

    fireEvent.change(screen.getByLabelText("Código de verificação"), { target: { value: "654321" } });
    fireEvent.click(screen.getByRole("button", { name: /redefinir senha/i }));

    expect(await screen.findByText("Senha redefinida!")).toBeInTheDocument();
    expect(body).toEqual({ token: "a".repeat(64), novaSenha: "SenhaForte123", codigoTotp: "654321" });
  });

  it("reenvio sem preencher o código TOTP não reenvia (AC-1.3)", async () => {
    let chamadas = 0;
    server.use(
      http.post("*/api/auth/reset-password", () => {
        chamadas += 1;
        return HttpResponse.json({ code: "mfa.codigo_invalido" }, { status: 422 });
      }),
    );

    renderWithProviders(<ResetPasswordPage />);
    await preencherSenha("SenhaForte123");
    await screen.findByLabelText("Código de verificação");

    fireEvent.click(screen.getByRole("button", { name: /redefinir senha/i }));

    expect(await screen.findByText("Informe o código de verificação.")).toBeInTheDocument();
    expect(chamadas).toBe(1);
  });

  it("reenvio com código inválido mantém o campo e mostra 'código inválido' (AC-2.1)", async () => {
    server.use(
      http.post("*/api/auth/reset-password", () =>
        HttpResponse.json({ code: "mfa.codigo_invalido" }, { status: 422 }),
      ),
    );

    renderWithProviders(<ResetPasswordPage />);
    await preencherSenha("SenhaForte123");
    await screen.findByLabelText("Código de verificação");

    fireEvent.change(screen.getByLabelText("Código de verificação"), { target: { value: "000000" } });
    fireEvent.click(screen.getByRole("button", { name: /redefinir senha/i }));

    expect(await screen.findByText("Código inválido.")).toBeInTheDocument();
    expect(screen.getByLabelText("Código de verificação")).toBeInTheDocument();
    expect(screen.queryByText("Senha redefinida!")).not.toBeInTheDocument();
  });

  it("auth_reset.segundo_fator_bloqueado mostra bloqueio + CTA para novo link (AC-2.2, EC-2)", async () => {
    server.use(
      http.post("*/api/auth/reset-password", () =>
        HttpResponse.json(
          { code: "auth_reset.segundo_fator_bloqueado", detail: "Muitas tentativas. Solicite um novo link." },
          { status: 422 },
        ),
      ),
    );

    renderWithProviders(<ResetPasswordPage />);
    await preencherSenha("SenhaForte123");

    expect(await screen.findByText("Muitas tentativas. Solicite um novo link.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /solicitar novo link/i })).toHaveAttribute(
      "href",
      "/forgot-password",
    );
  });

  it("token inválido/expirado mostra a mensagem do ProblemDetails (AC-2.3)", async () => {
    server.use(
      http.post("*/api/auth/reset-password", () =>
        HttpResponse.json({ title: "Token expirado. Solicite um novo link de redefinição." }, { status: 422 }),
      ),
    );

    renderWithProviders(<ResetPasswordPage />);
    await preencherSenha("SenhaForte123");

    expect(
      await screen.findByText("Token expirado. Solicite um novo link de redefinição."),
    ).toBeInTheDocument();
    expect(screen.queryByLabelText("Código de verificação")).not.toBeInTheDocument();
  });
});
