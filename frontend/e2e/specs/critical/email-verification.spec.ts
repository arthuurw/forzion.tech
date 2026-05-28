import { test, expect } from "../../fixtures/test-base";

/**
 * Critical 12/8+ — verify-email + resend-verification.
 *
 * Cobre:
 *   - Token inválido na URL → mensagem de erro.
 *   - Página /resend-verification valida e-mail e mostra confirmação.
 *
 * F23 (replay com mesmo token raw → 2ª chamada falha) é coberto a nível
 * unit em `VerificarEmailHandlerTests.HandleAsync_Replay_MesmoTokenDuasVezes_SegundaFalha_F23`.
 * O fluxo E2E replay requer hook `E2E_VERIFY_TOKEN_HOOK` (parallel ao do
 * password reset) para obter um token raw recém-emitido. Marcado como
 * `test.fixme()` até a infra existir.
 */

test.describe("auth: email verification", () => {
  test("/verify-email com token inválido → AlertBanner de erro", async ({ page }) => {
    await page.goto("/verify-email?token=token-claramente-invalido");

    await expect(
      page.getByRole("alert").filter({ hasText: /inválido|expirado|erro/i })
    ).toBeVisible({ timeout: 10_000 });
  });

  test("/verify-email sem token na URL → mostra erro ou redireciona", async ({ page }) => {
    await page.goto("/verify-email");

    // Página renderiza estado de erro (falta token) — não pode ficar "loading" infinito.
    await expect(
      page.getByRole("alert").or(page.getByText(/token|inválido|necessário/i))
    ).toBeVisible({ timeout: 10_000 });
  });

  test("/resend-verification com e-mail válido → tela de confirmação", async ({ page }) => {
    await page.goto("/resend-verification");

    await page.getByLabel(/e-?mail/i).fill(`user-${Date.now()}@e2e.test`);
    await page.getByRole("button", { name: /reenviar|enviar|continuar/i }).click();

    // Mesma anti-enumeração: sempre 200, mensagem genérica.
    await expect(
      page.getByRole("alert").or(page.getByText(/enviado|verifique|caixa de entrada/i))
    ).toBeVisible({ timeout: 10_000 });
  });

  test.fixme(
    "verify-email replay (mesmo raw token 2x) → 2ª chamada falha — depende de E2E_VERIFY_TOKEN_HOOK",
    async ({ page }) => {
      // Cobertura unit: VerificarEmailHandlerTests.HandleAsync_Replay_MesmoTokenDuasVezes_SegundaFalha_F23.
      // E2E aguarda hook que devolva o último token raw emitido para uma conta de teste.
      page.toString();
    }
  );
});
