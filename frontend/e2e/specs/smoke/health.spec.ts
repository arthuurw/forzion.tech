import { test, expect } from "../../fixtures/test-base";

/**
 * Smoke piloto Fase 9. Valida que a infra Playwright esta funcional:
 * - baseURL configurado
 * - navegacao basica funciona
 * - fixtures (consoleErrors) operam
 *
 * Tag @smoke permite filtrar via `npm run e2e:smoke`. Outros smokes vem em
 * Fase 10 (login, listagem alunos, criar+cleanup, checkout).
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
