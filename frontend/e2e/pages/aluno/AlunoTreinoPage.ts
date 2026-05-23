import type { Page, Locator } from "@playwright/test";
import { BasePage } from "../BasePage";

/**
 * POM de /aluno/fichas — lista as fichas atribuidas pelo treinador.
 */
export class AlunoFichasPage extends BasePage {
  readonly url = "/aluno/fichas";

  readonly heading: Locator;
  readonly fichaCards: Locator;
  readonly emptyState: Locator;

  constructor(page: Page) {
    super(page);
    this.heading = page.getByRole("heading", { name: /fichas/i });
    this.fichaCards = page.locator("[data-testid='ficha-card']");
    this.emptyState = page.getByText(/nenhuma ficha/i);
  }
}
