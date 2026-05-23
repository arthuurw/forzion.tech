import { test, expect, useAuthRole } from "../../fixtures/test-base";
import { AssinaturaPage } from "../../pages/aluno/AssinaturaPage";

/**
 * Smoke 5/5 — aluno acessa /aluno/assinatura e ve status da assinatura.
 *
 * NAO interage com Stripe Elements aqui (Fase 10a critical/checkout-stripe
 * cobre o fluxo completo). Smoke valida que a pagina carrega + status visivel.
 */

useAuthRole(test, "aluno");

test.describe("smoke @smoke", () => {
  test("aluno ve status da assinatura", async ({ page }) => {
    const assinatura = new AssinaturaPage(page);
    await assinatura.goto();

    // Ou tem assinatura (heading visivel) ou alerta informativo "nao possui"
    const hasAssinatura = await assinatura.heading.isVisible().catch(() => false);
    if (hasAssinatura) {
      await expect(assinatura.statusChip).toBeVisible();
    } else {
      await expect(assinatura.infoAlert).toBeVisible();
    }
  });
});
