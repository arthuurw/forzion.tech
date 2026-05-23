import type { Page, Response } from "@playwright/test";

/**
 * Base Page Object Model. Fase 9 estabelece a classe abstrata; Fase 10 popula
 * com LoginPage, AdminAlunoPage, CheckoutPage, etc.
 *
 * Convencao: cada page concreta extende BasePage, expoe `url` (path relativo
 * a baseURL) e `goto()` retorna Response da navegacao pra assertions.
 */
export abstract class BasePage {
  abstract readonly url: string;

  constructor(public readonly page: Page) {}

  async goto(): Promise<Response | null> {
    return this.page.goto(this.url);
  }

  async waitForLoad(): Promise<void> {
    await this.page.waitForLoadState("domcontentloaded");
  }
}
