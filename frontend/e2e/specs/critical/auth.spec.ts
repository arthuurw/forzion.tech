import { test, expect } from "../../fixtures/test-base";
import { LoginPage } from "../../pages/LoginPage";

/**
 * Critical 1/8 — auth flow: login OK, login KO, logout.
 *
 * NAO usa storageState (testa o login do zero). Requer creds admin.
 */

test.describe("auth flow", () => {
  test("login com credenciais validas redireciona pra home da role", async ({ page }) => {
    const email = process.env.E2E_ADMIN_EMAIL;
    const password = process.env.E2E_ADMIN_PASSWORD;
    expect(email).toBeTruthy();
    expect(password).toBeTruthy();

    const login = new LoginPage(page);
    await login.goto();
    await login.submit(email!, password!);
    await page.waitForURL(/\/(admin|aluno|treinador)/);
    expect(page.url()).toMatch(/\/(admin|aluno|treinador)/);
  });

  test("login com credenciais invalidas exibe erro 401", async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.submit("nao-existe@e2e.test", "SenhaErrada123!");
    await expect(login.errorBanner.filter({ hasText: /credenciais inválidas/i })).toBeVisible();
    expect(page.url()).toContain("/login");
  });

  test("logout limpa sessao e redireciona pra /login", async ({ page }) => {
    const email = process.env.E2E_ADMIN_EMAIL;
    const password = process.env.E2E_ADMIN_PASSWORD;
    expect(email).toBeTruthy();
    expect(password).toBeTruthy();

    const login = new LoginPage(page);
    await login.goto();
    await login.submit(email!, password!);
    await page.waitForURL(/\/admin/);

    // Abre menu do avatar e clica em Sair
    await page.getByRole("button").filter({ has: page.locator(".MuiAvatar-root") }).first().click();
    await page.getByRole("menuitem", { name: /sair/i }).click();

    await page.waitForURL(/\/login/);
    expect(page.url()).toContain("/login");
  });
});
