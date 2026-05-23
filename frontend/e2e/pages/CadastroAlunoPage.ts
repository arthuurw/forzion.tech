import type { Page, Locator } from "@playwright/test";
import { BasePage } from "./BasePage";

/**
 * POM de /cadastro/aluno — fluxo de stepper (4 steps).
 *
 * Step 0: escolher treinador
 * Step 1: escolher pacote
 * Step 2: dados pessoais (nome, email, telefone, senha, confirmar senha)
 * Step 3: disponibilidade + objetivos + saude + observacoes → submit
 */
export class CadastroAlunoPage extends BasePage {
  readonly url = "/cadastro/aluno";

  readonly heading: Locator;
  readonly carregarTreinadoresButton: Locator;
  readonly treinadorCards: Locator;
  readonly pacoteCards: Locator;

  readonly nomeInput: Locator;
  readonly emailInput: Locator;
  readonly telefoneInput: Locator;
  readonly passwordInput: Locator;
  readonly confirmPasswordInput: Locator;
  readonly nextStep2Button: Locator;

  readonly diasSelect: Locator;
  readonly tempoSelect: Locator;
  readonly finalidadeSelect: Locator;
  readonly nivelSelect: Locator;

  readonly submitButton: Locator;
  readonly successMessage: Locator;
  readonly errorBanner: Locator;

  constructor(page: Page) {
    super(page);
    this.heading = page.getByRole("heading", { name: /criar conta como aluno/i });
    this.carregarTreinadoresButton = page.getByRole("button", { name: /carregar treinadores/i });
    this.treinadorCards = page.locator(".MuiCard-root").filter({ has: page.locator('[type="radio"]') });
    this.pacoteCards = page.locator(".MuiCard-root").filter({ has: page.locator('[type="radio"]') });

    this.nomeInput = page.getByLabel(/nome completo/i);
    this.emailInput = page.getByLabel(/e-?mail/i);
    this.telefoneInput = page.getByLabel(/celular/i);
    this.passwordInput = page.getByLabel(/^senha/i);
    this.confirmPasswordInput = page.getByLabel(/confirmar senha/i);
    this.nextStep2Button = page.getByRole("button", { name: /próximo/i });

    this.diasSelect = page.getByLabel(/dias disponíveis/i);
    this.tempoSelect = page.getByLabel(/tempo disponível/i);
    this.finalidadeSelect = page.getByLabel(/finalidade do treino/i);
    this.nivelSelect = page.getByLabel(/nível de condicionamento/i);

    this.submitButton = page.getByRole("button", { name: /criar conta/i });
    this.successMessage = page.getByText(/solicitação de vínculo enviada/i);
    this.errorBanner = page.getByRole("alert");
  }

  async pickFirstTreinador(): Promise<void> {
    await this.treinadorCards.first().click();
  }

  async pickFirstPacote(): Promise<void> {
    await this.pacoteCards.first().click();
  }

  async fillDadosPessoais(data: {
    nome: string;
    email: string;
    telefone: string;
    senha: string;
  }): Promise<void> {
    await this.nomeInput.fill(data.nome);
    await this.emailInput.fill(data.email);
    await this.telefoneInput.fill(data.telefone);
    await this.passwordInput.fill(data.senha);
    await this.confirmPasswordInput.fill(data.senha);
  }

  async pickSelectOption(selectLocator: Locator, optionText: string): Promise<void> {
    await selectLocator.click();
    await this.page.getByRole("option", { name: optionText }).first().click();
  }
}
