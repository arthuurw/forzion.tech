import { test, expect } from "../../fixtures/test-base";
import { authStatePath, hasAuthState } from "../../fixtures/auth";

/**
 * Multi-tab 2/2 — sessao autenticada em uma aba se reflete em outra
 * recem-aberta no mesmo context.
 *
 * Estrategia: context carrega storage state admin. Abre pageA + pageB. Ambas
 * devem ter acesso a /admin sem redirect pra /login.
 */

test.describe("multi-tab: session sync", () => {
  test.beforeAll(() => {
    if (!hasAuthState("admin")) {
      throw new Error(
        "Storage state admin ausente. Rode `npx playwright test --project=setup` primeiro.",
      );
    }
  });

  test("duas abas com mesmo context acessam /admin sem redirect", async ({ browser }) => {
    const context = await browser.newContext({ storageState: authStatePath("admin") });
    const pageA = await context.newPage();
    const pageB = await context.newPage();

    await pageA.goto("/admin");
    await pageB.goto("/admin");

    expect(pageA.url(), "pageA nao deve redirecionar pra /login").not.toContain("/login");
    expect(pageB.url(), "pageB nao deve redirecionar pra /login").not.toContain("/login");

    await context.close();
  });
});
