import { test, expect } from "../../fixtures/test-base";

/**
 * Smoke 1/5 — landing page responde + zero console.error.
 *
 * Nao requer auth/creds. Roda em qualquer ambiente com app acessivel via
 * baseURL.
 */

test.describe("smoke @smoke", () => {
  test("landing page responde 2xx e renderiza body", async ({ page, consoleErrors }) => {
    const response = await page.goto("/");
    expect(response, "navegacao deve retornar Response").not.toBeNull();
    expect(response!.status(), `status HTTP de /`).toBeLessThan(400);

    await expect(page.locator("body")).toBeVisible();

    consoleErrors.assertNoErrors();
  });
});
