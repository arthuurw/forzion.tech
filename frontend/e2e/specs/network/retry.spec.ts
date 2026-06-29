import { test, expect, useAuthRole } from "../../fixtures/test-base";

/**
 * Network 3/3 — flakyRoute: 2 falhas iniciais + sucesso na 3a tentativa.
 *
 * Valida que o apiClient (axios) NAO faz retry automatico (decisao do
 * projeto). UI deve mostrar erro depois da primeira falha; refresh manual
 * recupera quando rede estabiliza.
 *
 * Spec garante regressao: se alguem adicionar interceptor de retry sem
 * teste, este spec detecta (UI mostraria sucesso direto, ignorando o erro
 * inicial).
 */

useAuthRole(test, "admin");

test.describe("network: retry behavior", () => {
  test("primeira request falha mostra erro; reload manual recupera", async ({
    page,
    network,
  }) => {
    const flaky = await network.flaky(/\/api\/backend\/admin\/alunos/, 1, 503);

    await page.goto("/admin/alunos");
    await page.waitForLoadState("domcontentloaded");

    await page.waitForFunction(() => true, { timeout: 5_000 }).catch(() => undefined);

    expect(flaky.attempts(), "primeira tentativa registrada").toBeGreaterThanOrEqual(1);

    const errorBanner = page.getByRole("alert").filter({ hasText: /erro/i });
    await expect(errorBanner.first()).toBeVisible({ timeout: 10_000 });

    await flaky.restore();
    await page.reload();
    await expect(page.getByRole("heading", { name: /^alunos$/i })).toBeVisible();
  });
});
