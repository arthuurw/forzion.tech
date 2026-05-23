import { test, expect, useAuthRole } from "../../fixtures/test-base";
import { AlunoFichasPage } from "../../pages/aluno/AlunoTreinoPage";

/**
 * Critical 4/8 — aluno acessa fichas atribuidas e abre uma para execucao.
 *
 * Requer aluno com pelo menos 1 ficha atribuida. Se nao houver, valida ao
 * menos a empty state pra evitar regressao silenciosa.
 */

useAuthRole(test, "aluno");

test.describe("aluno fichas", () => {
  test("lista fichas (ou empty state) sem erros", async ({ page, consoleErrors }) => {
    const fichas = new AlunoFichasPage(page);
    await fichas.goto();
    await expect(fichas.heading).toBeVisible();

    const hasFichas = (await fichas.fichaCards.count()) > 0;
    if (!hasFichas) {
      await expect(fichas.emptyState).toBeVisible();
    }

    consoleErrors.assertNoErrors();
  });

  test("abrir primeira ficha navega pro detalhe", async ({ page }) => {
    const fichas = new AlunoFichasPage(page);
    await fichas.goto();

    const count = await fichas.fichaCards.count();
    test.skip(count === 0, "aluno sem fichas atribuidas — nada pra abrir");

    await fichas.fichaCards.first().click();
    await page.waitForURL(/\/aluno\/fichas\/[^/]+/);
    expect(page.url()).toMatch(/\/aluno\/fichas\/[a-z0-9-]+/i);
  });
});
