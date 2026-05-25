import { test, expect, useAuthRole } from "../../fixtures/test-base";

useAuthRole(test, "treinador");

const PAGES = [
  { name: "treinador-dashboard", url: "/treinador" },
  { name: "treinador-alunos", url: "/treinador/alunos" },
];

test.describe("visual: treinador @visual", () => {
  for (const { name, url } of PAGES) {
    test(name, async ({ page }) => {
      await page.goto(url);
      await page.waitForLoadState("domcontentloaded");
      await page.evaluate(() => document.fonts.ready);
      await expect(page).toHaveScreenshot(`${name}.png`, { fullPage: true });
    });
  }
});
