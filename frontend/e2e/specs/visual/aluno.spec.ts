import { test, expect, useAuthRole } from "../../fixtures/test-base";

useAuthRole(test, "aluno");

const PAGES = [
  { name: "aluno-dashboard", url: "/aluno" },
  { name: "aluno-assinatura", url: "/aluno/assinatura" },
];

test.describe("visual: aluno @visual", () => {
  for (const { name, url } of PAGES) {
    test(name, async ({ page }) => {
      await page.goto(url);
      await page.waitForLoadState("domcontentloaded");
      await page.evaluate(() => document.fonts.ready);
      await expect(page).toHaveScreenshot(`${name}.png`, { fullPage: true });
    });
  }
});
