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

    // Espera a lista assentar: linha OU empty state. Sem o .or() o teste
    // amostraria rows.count() durante o spinner de loading (0) e cairia no
    // ramo errado quando há alunos que ainda estavam carregando.
    await expect(alunos.rows.first().or(alunos.emptyState)).toBeVisible();
  });
});
