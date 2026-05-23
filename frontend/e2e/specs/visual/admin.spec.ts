import { test, expect, useAuthRole } from "../../fixtures/test-base";

/**
 * Visual — paginas admin.
 *
 * Requer storage state admin. Snapshot inclui sidebar + header + conteudo.
 * Dados dinamicos (linhas da tabela) podem causar flake — assumimos seed
 * estavel em homolog ou snapshot tolerante via threshold (config global).
 */

useAuthRole(test, "admin");

const PAGES = [
  { name: "admin-dashboard", url: "/admin" },
  { name: "admin-alunos", url: "/admin/alunos" },
  { name: "admin-treinadores", url: "/admin/treinadores" },
];

test.describe("visual: admin @visual", () => {
  for (const { name, url } of PAGES) {
    test(name, async ({ page }) => {
      await page.goto(url);
      await page.waitForLoadState("domcontentloaded");
      await page.evaluate(() => document.fonts.ready);
      await expect(page).toHaveScreenshot(`${name}.png`, { fullPage: true });
    });
  }
});
