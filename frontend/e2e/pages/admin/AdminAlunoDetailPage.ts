import type { Page, Locator } from "@playwright/test";
import { BasePage } from "../BasePage";

export class AdminAlunoDetailPage extends BasePage {
  readonly url: string;

  readonly heading: Locator;
  readonly statusChip: Locator;
  readonly inativarButton: Locator;
  readonly ativarButton: Locator;
  readonly confirmDialog: Locator;

  constructor(page: Page, alunoId: string) {
    super(page);
    this.url = `/admin/alunos/${alunoId}`;
    this.heading = page.getByRole("heading").first();
    this.statusChip = page.locator('[data-testid="status-chip"]').first();
    this.inativarButton = page.getByRole("button", { name: /inativar/i });
    this.ativarButton = page.getByRole("button", { name: /ativar/i });
    this.confirmDialog = page.getByRole("dialog");
  }
}
