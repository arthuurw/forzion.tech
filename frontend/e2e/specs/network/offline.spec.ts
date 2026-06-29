import { test, expect, useAuthRole } from "../../fixtures/test-base";

/**
 * Network 2/3 — offline exibe banner de erro e recupera quando volta online.
 *
 * Estratégia: carregar lista admin → cortar conectividade → forçar reload da
 * lista (o hook `usePaginatedList` dispara fetch a cada navegação) → assertar
 * o `AlertBanner` (role="alert") com a mensagem específica que o hook seta.
 * Depois reconectar e reload — banner desaparece e a tabela volta.
 *
 * Antes desta versão o spec só tentava navegar e silenciava a falha com
 * `.catch(() => null)`, passando mesmo que a UI não tratasse o erro. O fix
 * para essa folga vem do plano de remediação F2.
 */

useAuthRole(test, "admin");

test.describe("network: offline", () => {
  test("offline → AlertBanner com 'Erro ao carregar alunos.' visível; online → some", async ({
    page,
    context,
  }) => {
    await page.goto("/admin/alunos");
    await page.waitForLoadState("domcontentloaded");
    // Não exigimos lista populada (depende de seed) — só que NÃO há banner de erro
    // antes do offline. `getByRole("alert")` é robusto contra mudança de classe MUI.
    await expect(page.getByRole("alert")).toHaveCount(0);

    // 2. Offline + força refetch via reload do hook (mudar página de paginação
    // dispara `usePaginatedList` reload; aqui usamos page.reload() que é mais
    // direto e independe de UI específica).
    await context.setOffline(true);
    await page.reload({ waitUntil: "domcontentloaded" }).catch(() => undefined);

    // 3. Assertion forte: o banner de erro aparece com a mensagem que o hook
    // realmente seta (não um texto inventado).
    await expect(
      page.getByRole("alert").filter({ hasText: "Erro ao carregar alunos." })
    ).toBeVisible({ timeout: 15_000 });

    await context.setOffline(false);
    await page.reload({ waitUntil: "domcontentloaded" });
    await expect(
      page.getByRole("alert").filter({ hasText: "Erro ao carregar alunos." })
    ).toHaveCount(0, { timeout: 15_000 });
  });
});
