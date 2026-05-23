import { test, expect } from "../../fixtures/test-base";

/**
 * Visual 1/1 — snapshot da tela de login (piloto).
 *
 * Visual regression usa `toHaveScreenshot()` com baselines **Linux-only**
 * (CI). NAO rode `--update-snapshots` localmente — fontes/OS diferentes
 * geram baselines que mascaram regressao real.
 *
 * Threshold (playwright.config.ts):
 * - maxDiffPixelRatio: 0.01 (1% de pixels diferentes)
 * - threshold: 0.2 (sensibilidade por pixel)
 *
 * Baselines geradas em Fase 17 (CI completo). Por ora, primeira run cria
 * baseline local (sera substituida quando CI rodar pela primeira vez).
 */

test.describe("visual: snapshots @visual", () => {
  test("login page", async ({ page }) => {
    await page.goto("/login");
    await page.waitForLoadState("domcontentloaded");
    // Espera fontes carregarem (jitter inicial reduz flake)
    await page.evaluate(() => document.fonts.ready);
    await expect(page).toHaveScreenshot("login.png", { fullPage: true });
  });
});
