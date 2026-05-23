import { test, expect, useAuthRole } from "../../fixtures/test-base";
import { AdminTreinadoresPage } from "../../pages/admin/AdminTreinadoresPage";

/**
 * Critical 3/8 — admin lista e filtra treinadores.
 *
 * Acoes write (aprovar/reprovar/inativar) sao destrutivas em homolog real —
 * cobrimos apenas leitura aqui. Fase futura pode adicionar specs com cleanup
 * via API.
 */

useAuthRole(test, "admin");

test.describe("admin treinador list", () => {
  test("lista treinadores e aplica filtro de status", async ({ page }) => {
    const treinadores = new AdminTreinadoresPage(page);
    await treinadores.goto();
    await expect(treinadores.heading).toBeVisible();

    await treinadores.filterByStatus("Ativo");

    // Espera que filtro nao quebre — pode estar vazio OK.
    await page.waitForLoadState("domcontentloaded");
  });

  test("filtro AguardandoAprovacao roda sem erro", async ({ page }) => {
    const treinadores = new AdminTreinadoresPage(page);
    await treinadores.goto();
    await treinadores.filterByStatus("Aguardando aprovação");
    await page.waitForLoadState("domcontentloaded");
    await expect(treinadores.errorBanner.filter({ hasText: /erro/i })).toBeHidden();
  });
});
