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

  test("finalizar offline enfileira e reenvia ao reconectar (EXOFF-11/12)", async ({
    page,
    network,
    browserName,
  }) => {
    test.skip(browserName !== "chromium", "depende de toggle online/offline do CDP/contexto");

    const fichas = new AlunoFichasPage(page);
    await fichas.goto();
    await expect(fichas.heading).toBeVisible();

    const rows = page.locator("tbody tr[role='button']");
    const count = await rows.count();
    test.skip(count === 0, "aluno sem fichas atribuidas — nada pra executar");

    await rows.first().click();
    await page.waitForURL(/\/aluno\/fichas\/[a-z0-9-]+$/i);

    const iniciar = page.getByRole("button", { name: "Iniciar treino" });
    test.skip(!(await iniciar.isVisible().catch(() => false)), "ficha inativa — sem iniciar");
    await iniciar.click();
    await page.waitForURL(/\/executar$/);

    const finalizar = page.getByRole("button", { name: /Finalizar treino/i });
    const proximo = page.getByRole("button", { name: /^Próximo$/ });
    for (let i = 0; i < 50 && !(await finalizar.isVisible().catch(() => false)); i++) {
      await proximo.click();
    }
    await expect(finalizar).toBeVisible();

    await network.offline();
    await finalizar.click();
    await page.getByRole("button", { name: /Confirmar registro/i }).click();

    await expect(page.getByText(/salva no aparelho/i)).toBeVisible();
    const enfileirado = await page.evaluate(() => localStorage.getItem("exec-queue"));
    expect(JSON.parse(enfileirado ?? "[]")).toHaveLength(1);

    await network.online();
    await expect
      .poll(() => page.evaluate(() => localStorage.getItem("exec-queue")), { timeout: 15000 })
      .toBeNull();

    await page.goto("/aluno/historico");
    await expect(page.getByRole("heading", { name: /hist(ó|o)rico/i })).toBeVisible();
  });
});
