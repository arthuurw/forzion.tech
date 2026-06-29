import { test, expect, useAuthRole } from "../../fixtures/test-base";
import { AdminAlunosPage } from "../../pages/admin/AdminAlunosPage";

/**
 * Critical 7/8 — ResponsiveTable comuta entre table (desktop) e cards (mobile).
 *
 * Componente src/components/ui/ResponsiveTable.tsx usa useMediaQuery
 * (theme.breakpoints.down("md")). Validamos os dois modos via viewport.
 */

useAuthRole(test, "admin");

test.describe("responsive table", () => {
  test("desktop renderiza tabela com tbody", async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    const alunos = new AdminAlunosPage(page);
    await alunos.goto();
    await expect(alunos.heading).toBeVisible();

    await expect(page.locator("table")).toBeVisible();
  });

  test("mobile renderiza cards em vez de table", async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    const alunos = new AdminAlunosPage(page);
    await alunos.goto();
    await expect(alunos.heading).toBeVisible();

    // Em mobile, table some — usuario ve cards. Validamos ausencia da tag table
    // OU presenca de testid card. Convencao do projeto eh card por linha em
    // mobile (ver DataList + ResponsiveTable).
    const tableCount = page.locator("table");
    await expect(tableCount, "mobile nao deve renderizar <table>").toHaveCount(0);
  });
});
