import { test, expect } from "../../fixtures/test-base";

/**
 * Critical 11/8+ — cadastro de treinador, fluxo de aquisição de receita.
 *
 * Cobre: validação client-side, submissão real ao backend, tela de sucesso
 * (status "Solicitação enviada — em análise"), e propagação de erro 4xx.
 *
 * Sem este spec, todo o caminho de signup de treinador (form, schema Zod,
 * proxy `/api/auth/register/treinador`, criação de Conta + Treinador pendente,
 * disparo de email de verificação) ficava sem cobertura E2E — só unit/integration.
 *
 * Não testa o fluxo completo de verify-email aqui (coberto em
 * `email-verification.spec.ts`). Foco: até "Solicitação enviada".
 */

test.describe("auth: cadastro de treinador", () => {
  test("validação client-side bloqueia submit com senha fraca", async ({ page }) => {
    await page.goto("/cadastro/treinador");

    await page.getByLabel(/nome completo/i).fill("Treinador Teste");
    await page.getByLabel(/^e-?mail$/i).fill(`treinador-${Date.now()}@e2e.test`);
    await page.getByLabel(/^senha$/i).fill("fraca");
    await page.getByLabel(/confirmar senha/i).fill("fraca");
    await page.getByRole("button", { name: /criar conta|enviar|cadastrar/i }).click();

    // Não navega — fica no /cadastro com erro inline do react-hook-form.
    expect(page.url()).toContain("/cadastro/treinador");
    await expect(
      page.getByText(/8 caracteres|maiúscula|minúscula|dígito/i).first()
    ).toBeVisible();
  });

  test("cadastro com senhas diferentes bloqueia submit", async ({ page }) => {
    await page.goto("/cadastro/treinador");

    await page.getByLabel(/nome completo/i).fill("Treinador Teste");
    await page.getByLabel(/^e-?mail$/i).fill(`treinador-${Date.now()}@e2e.test`);
    await page.getByLabel(/^senha$/i).fill("SenhaForte123");
    await page.getByLabel(/confirmar senha/i).fill("Diferente456!");
    await page.getByRole("button", { name: /criar conta|enviar|cadastrar/i }).click();

    expect(page.url()).toContain("/cadastro/treinador");
    await expect(page.getByText(/senhas? não conferem|não coincidem/i)).toBeVisible();
  });

  test("payload válido → tela 'Solicitação enviada'", async ({ page }) => {
    await page.goto("/cadastro/treinador");

    const email = `treinador-${Date.now()}@e2e.test`;
    await page.getByLabel(/nome completo/i).fill("Treinador E2E");
    await page.getByLabel(/^e-?mail$/i).fill(email);
    await page.getByLabel(/whatsapp/i).fill("11999999999");
    await page.getByLabel(/^senha$/i).fill("SenhaForte123");
    await page.getByLabel(/confirmar senha/i).fill("SenhaForte123");

    await page.getByRole("button", { name: /criar conta|enviar|cadastrar/i }).click();

    // Página de sucesso (success state do componente).
    await expect(page.getByText(/solicitação enviada/i)).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText(/em análise|aguarde|validação/i)).toBeVisible();
    await expect(page.getByRole("link", { name: /ir para o login|login/i })).toBeVisible();
  });

  test("e-mail já cadastrado → AlertBanner com mensagem 4xx", async ({ page }) => {
    // E-mail conhecido pré-existente (env aponta pro admin/aluno seed). Reutilizar
    // E2E_ADMIN_EMAIL: já existe no DB → backend deve responder 409/422.
    const email = process.env.E2E_ADMIN_EMAIL;
    test.skip(!email, "E2E_ADMIN_EMAIL não configurado");

    await page.goto("/cadastro/treinador");
    await page.getByLabel(/nome completo/i).fill("Treinador Duplicado");
    await page.getByLabel(/^e-?mail$/i).fill(email!);
    await page.getByLabel(/^senha$/i).fill("SenhaForte123");
    await page.getByLabel(/confirmar senha/i).fill("SenhaForte123");
    await page.getByRole("button", { name: /criar conta|enviar|cadastrar/i }).click();

    await expect(page.getByRole("alert").filter({ hasText: /já|existe|cadastrado/i })).toBeVisible({
      timeout: 10_000,
    });
  });
});
