import type { Page, Locator } from "@playwright/test";
import { BasePage } from "../BasePage";

/**
 * POM de /aluno/assinatura — status da assinatura + pagamento pendente.
 */
export class AssinaturaPage extends BasePage {
  readonly url = "/aluno/assinatura";

  readonly heading: Locator;
  readonly statusChip: Locator;
  readonly pagarAgoraButton: Locator;
  readonly errorAlert: Locator;
  readonly infoAlert: Locator;

  constructor(page: Page) {
    super(page);
    this.heading = page.getByRole("heading", { name: /minha assinatura/i });
    this.statusChip = page.locator(".MuiChip-label").first();
    this.pagarAgoraButton = page.getByRole("button", { name: /pagar agora/i });
    this.errorAlert = page.locator('[role="alert"]').filter({ hasText: /erro/i });
    this.infoAlert = page.getByText(/não possui/i);
  }
}
