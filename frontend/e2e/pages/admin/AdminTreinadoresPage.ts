import type { Page, Locator } from "@playwright/test";
import { BasePage } from "../BasePage";

/**
 * POM de /admin/treinadores. Lista + filtros + acoes (aprovar/reprovar/inativar/excluir).
 */
export class AdminTreinadoresPage extends BasePage {
  readonly url = "/admin/treinadores";

  readonly heading: Locator;
  readonly statusFilter: Locator;
  readonly rows: Locator;
  readonly errorBanner: Locator;

  constructor(page: Page) {
    super(page);
    this.heading = page.getByRole("heading", { name: /treinadores/i });
    this.statusFilter = page.getByLabel(/status/i).first();
    this.rows = page.locator("tbody tr");
    this.errorBanner = page.getByRole("alert");
  }

  async filterByStatus(status: string): Promise<void> {
    await this.statusFilter.click();
    await this.page.getByRole("option", { name: status }).click();
  }
}
