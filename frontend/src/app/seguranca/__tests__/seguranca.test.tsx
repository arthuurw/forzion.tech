import { describe, it, expect, beforeEach, vi } from "vitest";
import { screen, fireEvent, waitFor, within } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
  useParams: () => ({}),
}));

import SegurancaPage from "../page";
import { renderWithProviders } from "@/test/render";

function statusHandler(habilitado: boolean, extras: Record<string, unknown> = {}) {
  return http.get("*/conta/mfa/status", () =>
    HttpResponse.json({ habilitado, recoveryCodesRestantes: habilitado ? 8 : 0, dispositivos: [], ...extras }),
  );
}

beforeEach(() => {
  server.use(statusHandler(false));
});

describe("/seguranca — ativação", () => {
  it("ativa 2FA: mostra QR/chave e, após confirmar, exibe códigos de recuperação", async () => {
    server.use(
      http.post("*/conta/mfa/totp/iniciar", () =>
        HttpResponse.json({ secretBase32: "JBSWY3DPEHPK3PXP", otpauthUri: "otpauth://totp/forzion?secret=JBSWY3DPEHPK3PXP" }),
      ),
      http.post("*/conta/mfa/totp/confirmar", () =>
        HttpResponse.json({ recoveryCodes: ["AAAA-1111", "BBBB-2222", "CCCC-3333"] }),
      ),
    );

    renderWithProviders(<SegurancaPage />);

    fireEvent.click(await screen.findByRole("button", { name: /ativar 2fa/i }));

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByDisplayValue("JBSWY3DPEHPK3PXP")).toBeInTheDocument();

    fireEvent.change(within(dialog).getByLabelText("Código do aplicativo"), { target: { value: "123456" } });
    fireEvent.click(within(dialog).getByRole("button", { name: /confirmar/i }));

    expect(await screen.findByText("AAAA-1111")).toBeInTheDocument();
    expect(screen.getByText("CCCC-3333")).toBeInTheDocument();
  });

  it("código inválido na ativação exibe erro mapeado do backend", async () => {
    server.use(
      http.post("*/conta/mfa/totp/iniciar", () =>
        HttpResponse.json({ secretBase32: "JBSWY3DPEHPK3PXP", otpauthUri: "otpauth://x" }),
      ),
      http.post("*/conta/mfa/totp/confirmar", () =>
        HttpResponse.json({ detail: "Código TOTP inválido." }, { status: 422 }),
      ),
    );

    renderWithProviders(<SegurancaPage />);
    fireEvent.click(await screen.findByRole("button", { name: /ativar 2fa/i }));
    const dialog = await screen.findByRole("dialog");
    fireEvent.change(within(dialog).getByLabelText("Código do aplicativo"), { target: { value: "000000" } });
    fireEvent.click(within(dialog).getByRole("button", { name: /confirmar/i }));

    expect(await screen.findByText("Código TOTP inválido.")).toBeInTheDocument();
  });
});

describe("/seguranca — desativação", () => {
  it("desativar pede step-up e envia o token no header", async () => {
    let headerEnviado: string | null = "missing";
    server.use(
      statusHandler(true),
      http.post("*/auth/step-up/iniciar", () => HttpResponse.json({ fator: 0 })),
      http.post("*/auth/step-up/verificar", () =>
        HttpResponse.json({ token: "su-token-xyz", expiraEm: "2026-06-17T13:00:00Z" }),
      ),
      http.post("*/conta/mfa/desabilitar", ({ request }) => {
        headerEnviado = request.headers.get("X-Step-Up-Token");
        return new HttpResponse(null, { status: 204 });
      }),
    );

    renderWithProviders(<SegurancaPage />);

    fireEvent.click(await screen.findByRole("button", { name: /desativar 2fa/i }));

    const dialog = await screen.findByRole("dialog");
    expect(await within(dialog).findByText(/aplicativo autenticador/i)).toBeInTheDocument();
    fireEvent.change(within(dialog).getByLabelText("Código de verificação"), { target: { value: "654321" } });
    fireEvent.click(within(dialog).getByRole("button", { name: /^confirmar$/i }));

    await waitFor(() => expect(headerEnviado).toBe("su-token-xyz"));
    expect(await screen.findByText(/desativada/i)).toBeInTheDocument();
  });
});
