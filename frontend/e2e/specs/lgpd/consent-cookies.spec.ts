import { test, expect } from "../../fixtures/test-base";

/**
 * LGPD 1/3 — banner de consentimento de cookies + persistencia da escolha.
 *
 * **Status: feature ausente no app**. Banner LGPD nao implementado.
 *
 * Fase 10b mantem spec como **fail-loud skeleton**: quando rodado, falha com
 * mensagem clara, forcando decisao consciente (implementar banner OU remover
 * spec). Specs nao rodam em CI atual; falham quando Fase 17 ligar E2E.
 *
 * Quando feature for implementada, expandir testes:
 * - banner aparece em primeira visita
 * - aceitar/recusar persiste escolha em cookie
 * - escolha persiste entre sessoes
 * - link pra politica de privacidade visivel
 */

test.describe("LGPD: cookie consent", () => {
  test("banner de consentimento aparece em primeira visita", async ({ page, context }) => {
    await context.clearCookies();
    await page.goto("/");

    const banner = page.getByRole("dialog", { name: /cookie|consentimento|lgpd/i });
    await expect(
      banner,
      "banner LGPD ausente — implementar componente de consentimento de cookies",
    ).toBeVisible({ timeout: 5_000 });
  });
});
