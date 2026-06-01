import { test, expect } from "../../fixtures/test-base";

/**
 * Critical 9/8+ — Password reset flow end-to-end.
 *
 * Fluxo coberto:
 *   1. /forgot-password → submit e-mail → 200 silencioso (anti-enumeração).
 *   2. /forgot-password → e-mail inexistente → também 200 (não revela ausência).
 *   3. /reset-password?token=... → token inválido → mensagem de erro.
 *   4. Replay: tenta usar mesmo token 2x → 2ª falha com "inválido ou já utilizado".
 *
 * Pré-requisitos (env):
 *   - `E2E_RESET_EMAIL` — conta existente cuja senha pode ser alterada nos testes.
 *   - `E2E_RESET_TOKEN_HOOK` (opcional) — endpoint que retorna o último raw token
 *     emitido para uma conta de teste (necessário pra fluxo completo de reset).
 *     Sem ele, esse spec valida apenas os pontos sem dependência de e-mail real.
 *
 * Sem `E2E_RESET_TOKEN_HOOK` setado, os passos 4+ ficam como `test.fixme()` em
 * vez de `skip` — a presença force a CI a manter o gap visível até o hook
 * existir. Fluxo unit/handler já cobre a lógica (RedefinirSenhaHandlerTests).
 */

test.describe("auth: password reset", () => {
  test("forgot-password com e-mail válido → 200 silencioso", async ({ page }) => {
    await page.goto("/forgot-password");

    const email = process.env.E2E_RESET_EMAIL ?? "user-reset@e2e.test";
    await page.getByLabel(/e-?mail/i).fill(email);
    await page.getByRole("button", { name: /enviar|recuperar|continuar/i }).click();

    // Página de sucesso ou banner de confirmação genérico (não revela existência).
    await expect(page.getByRole("alert").or(page.getByText(/enviado|verifique/i))).toBeVisible({
      timeout: 10_000,
    });
  });

  test("forgot-password com e-mail inexistente → mesmo 200 (anti-enumeração)", async ({ page }) => {
    await page.goto("/forgot-password");

    await page.getByLabel(/e-?mail/i).fill(`ghost-${Date.now()}@nao-existe.test`);
    await page.getByRole("button", { name: /enviar|recuperar|continuar/i }).click();

    // Idêntico ao caso válido — fluxo NÃO pode diferenciar a existência.
    await expect(page.getByRole("alert").or(page.getByText(/enviado|verifique/i))).toBeVisible({
      timeout: 10_000,
    });
  });

  test("reset-password com token inválido → erro", async ({ page }) => {
    await page.goto("/reset-password?token=token-claramente-invalido");

    await page.getByLabel(/nova senha/i).fill("NovaSenha123");
    await page.getByLabel(/confirmar/i).fill("NovaSenha123");
    await page.getByRole("button", { name: /redefinir|confirmar|alterar/i }).click();

    await expect(
      page.getByRole("alert").filter({ hasText: /inválido|expirado/i })
    ).toBeVisible({ timeout: 10_000 });
  });

  test.fixme(
    "reset-password replay (mesmo token 2x) → 2ª chamada falha com 'já utilizado'",
    async ({ page }) => {
      // TODO: depende de E2E_RESET_TOKEN_HOOK pra obter um token raw real recém-emitido.
      // Cobertura unit/handler já existe (RedefinirSenhaHandlerTests.HandleAsync_Replay).
      // Habilitar este teste quando o hook estiver disponível em homolog.
      const tokenHook = process.env.E2E_RESET_TOKEN_HOOK;
      expect(tokenHook).toBeTruthy();

      // const rawToken = await fetch(`${tokenHook}?email=${E2E_RESET_EMAIL}`).then(r => r.text());
      // await page.goto(`/reset-password?token=${rawToken}`);
      // ... primeira submissão sucede
      // await page.goto(`/reset-password?token=${rawToken}`);
      // ... segunda submissão deve falhar com "inválido ou já utilizado"
      page.toString();
    }
  );
});
