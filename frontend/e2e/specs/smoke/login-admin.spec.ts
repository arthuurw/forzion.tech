import { test, expect } from "../../fixtures/test-base";
import { LoginPage } from "../../pages/LoginPage";
import { seedConsent } from "../../utils/consent";

/**
 * Smoke 2/5 — admin login via UI redireciona pra /admin.
 *
 * Requer E2E_ADMIN_EMAIL / E2E_ADMIN_PASSWORD. Fase 10a fail loud — sem creds
 * o assertion falha e bloqueia merge.
 */

test.describe("smoke @smoke", () => {
  test("admin loga via UI e cai em /admin", async ({ page, context }) => {
    const email = process.env.E2E_ADMIN_EMAIL;
    const password = process.env.E2E_ADMIN_PASSWORD;

    expect(email, "E2E_ADMIN_EMAIL ausente").toBeTruthy();
    expect(password, "E2E_ADMIN_PASSWORD ausente").toBeTruthy();

    await seedConsent(context);
    const login = new LoginPage(page);
    await login.goto();
    await expect(login.heading).toBeVisible();

    await login.submit(email!, password!);

    await page.waitForURL(/\/admin/);
    expect(page.url()).toMatch(/\/admin/);
  });
});
