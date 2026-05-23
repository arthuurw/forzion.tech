import type { Page, Locator } from "@playwright/test";
import { BasePage } from "./BasePage";

/**
 * POM da tela /login.
 *
 * Selectors baseados em src/app/(public)/login/page.tsx + FormTextField/PasswordField.
 */
export class LoginPage extends BasePage {
  readonly url = "/login";

  readonly emailInput: Locator;
  readonly passwordInput: Locator;
  readonly submitButton: Locator;
  readonly errorBanner: Locator;
  readonly heading: Locator;

  constructor(page: Page) {
    super(page);
    this.heading = page.getByRole("heading", { name: /acesse sua conta/i });
    this.emailInput = page.getByLabel(/e-?mail/i).first();
    this.passwordInput = page.getByLabel(/senha/i).first();
    this.submitButton = page.getByRole("button", { name: /entrar/i });
    this.errorBanner = page.getByRole("alert");
  }

  async submit(email: string, password: string): Promise<void> {
    await this.emailInput.fill(email);
    await this.passwordInput.fill(password);
    await this.submitButton.click();
  }
}
