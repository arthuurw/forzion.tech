import type { Page, Locator, FrameLocator } from "@playwright/test";
import { BasePage } from "./BasePage";

/**
 * POM do componente PagamentoCartao (Stripe Elements em iframe).
 *
 * Stripe Elements injeta iframes — interagimos via frameLocator. Cada campo
 * (numero, validade, cvc) pode estar em iframes separados (dependendo do
 * PaymentElement layout).
 */
export class CheckoutPage extends BasePage {
  readonly url = "/aluno/assinatura";

  readonly heading: Locator;
  readonly amount: Locator;
  readonly payButton: Locator;
  readonly errorAlert: Locator;
  readonly successMessage: Locator;
  readonly stripeFrame: FrameLocator;

  constructor(page: Page) {
    super(page);
    this.heading = page.getByRole("heading", { name: /pagamento com cartão/i });
    this.amount = page.locator("text=/^R\\$\\s/");
    this.payButton = page.getByRole("button", { name: /^pagar$/i });
    this.errorAlert = page.locator('[role="alert"]').filter({ hasText: /erro|falhou|recusado/i });
    this.successMessage = page.getByText(/pagamento confirmado/i);
    this.stripeFrame = page.frameLocator("iframe[name^='__privateStripeFrame']").first();
  }

  /**
   * Preenche cartao no PaymentElement. Stripe layout pode variar — tentamos
   * seletores comuns via getByLabel.
   */
  async fillCard(opts: { number: string; expiry: string; cvc: string; zip?: string }): Promise<void> {
    const frame = this.stripeFrame;
    await frame.getByLabel(/card number|número do cartão/i).fill(opts.number);
    await frame.getByLabel(/expiration|validade|exp/i).fill(opts.expiry);
    await frame.getByLabel(/^cvc$|^cvv$/i).fill(opts.cvc);
    if (opts.zip) {
      const zipField = frame.getByLabel(/zip|postal|cep/i);
      if (await zipField.count()) await zipField.fill(opts.zip);
    }
  }
}
