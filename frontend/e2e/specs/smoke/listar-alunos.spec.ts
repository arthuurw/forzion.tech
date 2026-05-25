import { test, expect, useAuthRole } from "../../fixtures/test-base";
import { AdminAlunosPage } from "../../pages/admin/AdminAlunosPage";

/**
 * Smoke 3/5 — admin abre /admin/alunos e ve tabela carregada (ou empty state).
 */

useAuthRole(test, "admin");

test.describe("smoke @smoke", () => {
  test("admin lista alunos", async ({ page }) => {
    const alunos = new AdminAlunosPage(page);
    await alunos.goto();
    await expect(alunos.heading).toBeVisible();
    await expect(alunos.nomeFilter).toBeVisible();

    // Ou tem linhas, ou empty state — qualquer um eh smoke OK.
    const hasRows = (await alunos.rows.count()) > 0;
    if (!hasRows) {
      await expect(alunos.emptyState).toBeVisible();
    }
  });
});
