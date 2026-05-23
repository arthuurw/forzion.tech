import type { Page, Locator } from "@playwright/test";
import { BasePage } from "../BasePage";

/**
 * POM de /admin/alunos. Baseado em src/app/(admin)/admin/alunos/page.tsx
 * (componentes DataList + ResponsiveTable).
 */
export class AdminAlunosPage extends BasePage {
  readonly url = "/admin/alunos";

  readonly heading: Locator;
  readonly nomeFilter: Locator;
  readonly statusFilter: Locator;
  readonly rows: Locator;
  readonly emptyState: Locator;
  readonly errorBanner: Locator;

  constructor(page: Page) {
    super(page);
    this.heading = page.getByRole("heading", { name: /^alunos$/i });
    this.nomeFilter = page.getByLabel(/buscar por nome/i);
    this.statusFilter = page.getByLabel(/status/i).first();
    this.rows = page.locator("tbody tr");
    this.emptyState = page.getByText(/nenhum aluno encontrado/i);
    this.errorBanner = page.getByRole("alert");
  }

  async filterByNome(nome: string): Promise<void> {
    await this.nomeFilter.fill(nome);
    await this.nomeFilter.press("Enter");
  }

  async openDetail(rowIndex: number): Promise<void> {
    const row = this.rows.nth(rowIndex);
    await row.getByRole("button", { name: /ver detalhe/i }).click();
  }
}
