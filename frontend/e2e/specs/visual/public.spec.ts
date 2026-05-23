import { test, expect } from "../../fixtures/test-base";

/**
 * Visual — paginas publicas: landing + cadastros.
 *
 * Baselines Linux only (CI Fase 17). Nao rode --update-snapshots local.
 */

const PAGES = [
  { name: "landing", url: "/" },
  { name: "cadastro-aluno", url: "/cadastro/aluno" },
  { name: "cadastro-treinador", url: "/cadastro/treinador" },
];

test.describe("visual: publicas @visual", () => {
  for (const { name, url } of PAGES) {
    test(name, async ({ page }) => {
      await page.goto(url);
      await page.waitForLoadState("domcontentloaded");
      await page.evaluate(() => document.fonts.ready);
      await expect(page).toHaveScreenshot(`${name}.png`, { fullPage: true });
    });
  }
});
