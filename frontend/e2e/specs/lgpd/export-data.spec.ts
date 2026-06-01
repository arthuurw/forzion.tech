import { test, expect, useAuthRole } from "../../fixtures/test-base";

/**
 * LGPD 3/3 — exportacao de dados pessoais (direito de portabilidade).
 *
 * **Status: feature ausente**. Endpoint `/api/perfil/exportar` nao existe.
 * Fase 10b mantem skeleton fail-loud.
 *
 * Quando feature for implementada:
 * - Botao "Exportar meus dados" em /perfil
 * - GET /api/perfil/exportar retorna JSON (ou ZIP com .json + planilhas)
 * - Conteudo inclui: dados pessoais, fichas, execucoes, pagamentos
 * - Download via Content-Disposition: attachment
 */

useAuthRole(test, "aluno");

test.describe("LGPD: export data", () => {
  test("aluno consegue exportar dados em /perfil", async ({ page }) => {
    await page.goto("/perfil");

    // Dispensa o banner de consentimento (modal, dynamic-import) que cobre a página no
    // primeiro acesso — Playwright auto-espera o botão aparecer.
    await page.getByRole("button", { name: /só essenciais/i }).click();

    const exportButton = page.getByRole("button", {
      name: /exportar.*dados|baixar.*meus dados/i,
    });
    await expect(
      exportButton,
      "botao exportar dados ausente — implementar feature LGPD portabilidade",
    ).toBeVisible({ timeout: 5_000 });
  });
});
