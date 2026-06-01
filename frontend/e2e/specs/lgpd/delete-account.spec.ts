import { test, expect, useAuthRole } from "../../fixtures/test-base";

/**
 * LGPD 2/3 — exclusao de conta (direito ao esquecimento).
 *
 * **Status: feature ausente**. Endpoint `/api/perfil/excluir` ou similar nao
 * existe. Fase 10b mantem skeleton fail-loud.
 *
 * Quando feature for implementada:
 * - Botao "Excluir minha conta" em /perfil
 * - Confirmacao em modal (re-autenticacao com senha)
 * - DELETE /api/perfil retorna 204
 * - Subsequente login com mesmo email retorna 401
 * - Dados pessoais removidos (verificar via API admin antes/depois)
 */

useAuthRole(test, "aluno");

test.describe("LGPD: delete account", () => {
  test("aluno consegue excluir propria conta via /perfil", async ({ page }) => {
    await page.goto("/perfil");

    // Dispensa o banner de consentimento (modal, dynamic-import) que cobre a página no
    // primeiro acesso — Playwright auto-espera o botão aparecer.
    await page.getByRole("button", { name: /só essenciais/i }).click();

    const deleteButton = page.getByRole("button", { name: /excluir.*conta|deletar conta/i });
    await expect(
      deleteButton,
      "botao excluir conta ausente — implementar feature LGPD direito ao esquecimento",
    ).toBeVisible({ timeout: 5_000 });
  });
});
