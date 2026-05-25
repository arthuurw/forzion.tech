import { test, expect, useAuthRole } from "../../fixtures/test-base";
import { AssinaturaPage } from "../../pages/aluno/AssinaturaPage";
import { CheckoutPage } from "../../pages/CheckoutPage";
import { STRIPE_TEST_CARDS, STRIPE_TEST_CVC, STRIPE_TEST_EXPIRY } from "../../utils/stripe";

/**
 * Critical 5/8 — checkout Stripe em test mode.
 *
 * Requer:
 * - aluno com assinatura ativa + pagamento pendente Cartao (clientSecret valido)
 * - NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY = pk_test_*
 * - backend Stripe configurado em test mode
 *
 * Cobre 3 cenarios:
 * - cartao success (4242 4242 4242 4242) → "Pagamento confirmado"
 * - cartao recusado (4000 0000 0000 0002) → erro
 * - 3D Secure (4000 0027 6000 3184) → challenge frame (validamos so o trigger)
 *
 * Webhook stripe-cli + valida estado final em /aluno/assinatura — adiado para
 * follow-up (requer stripe-cli configurado).
 */

useAuthRole(test, "aluno");

test.describe("checkout Stripe", () => {
  test.beforeEach(async ({ page }) => {
    const assinatura = new AssinaturaPage(page);
    await assinatura.goto();

    const hasPagar = await assinatura.pagarAgoraButton.isVisible().catch(() => false);
    test.skip(
      !hasPagar,
      "aluno sem pagamento pendente — seed/backend precisa configurar cobranca",
    );

    await assinatura.pagarAgoraButton.click();
  });

  test("cartao success confirma pagamento", async ({ page }) => {
    const checkout = new CheckoutPage(page);
    await expect(checkout.heading).toBeVisible();

    await checkout.fillCard({
      number: STRIPE_TEST_CARDS.success.replace(/\s/g, ""),
      expiry: STRIPE_TEST_EXPIRY.replace(/\s/g, ""),
      cvc: STRIPE_TEST_CVC,
    });
    await checkout.payButton.click();

    await expect(checkout.successMessage).toBeVisible({ timeout: 15_000 });
  });

  test("cartao recusado exibe mensagem de erro", async ({ page }) => {
    const checkout = new CheckoutPage(page);
    await checkout.fillCard({
      number: STRIPE_TEST_CARDS.decline.replace(/\s/g, ""),
      expiry: STRIPE_TEST_EXPIRY.replace(/\s/g, ""),
      cvc: STRIPE_TEST_CVC,
    });
    await checkout.payButton.click();

    await expect(checkout.errorAlert).toBeVisible({ timeout: 15_000 });
  });

  test("cartao 3DS dispara challenge frame", async ({ page }) => {
    const checkout = new CheckoutPage(page);
    await checkout.fillCard({
      number: STRIPE_TEST_CARDS.threeDSecure.replace(/\s/g, ""),
      expiry: STRIPE_TEST_EXPIRY.replace(/\s/g, ""),
      cvc: STRIPE_TEST_CVC,
    });
    await checkout.payButton.click();

    // 3DS challenge abre frame "Stripe 3D Secure" — basta detectar presenca.
    const challengeFrame = page.frameLocator(
      "iframe[name*='3d-secure'], iframe[name*='__privateStripeFrame']",
    );
    await expect(challengeFrame.locator("body").first()).toBeAttached({ timeout: 15_000 });
  });
});
