import { test, expect } from "../../fixtures/test-base";
import { authStatePath, hasAuthState } from "../../fixtures/auth";

/**
 * Multi-tab 1/2 — logout em uma aba propaga pra outra (cookie invalidado).
 *
 * Cria 2 contexts compartilhando storage state admin. Faz logout no context A
 * (POST /api/auth/logout limpa cookies). Em context B, proximo request pra
 * rota protegida deve retornar 401, e UI redireciona pra /login.
 *
 * Nota: Playwright BrowserContexts sao isolados (cookies independentes).
 * Cross-tab real testa via mesma context com 2 pages — cookies sao
 * compartilhadas no mesmo context.
 */

test.describe("multi-tab: logout cross-tab", () => {
  test.beforeAll(() => {
    if (!hasAuthState("admin")) {
      throw new Error(
        "Storage state admin ausente. Rode `npx playwright test --project=setup` primeiro.",
      );
    }
  });

  test("logout em pagina A invalida sessao em pagina B (mesmo context)", async ({ browser }) => {
    const context = await browser.newContext({ storageState: authStatePath("admin") });
    const pageA = await context.newPage();
    const pageB = await context.newPage();

    await pageA.goto("/admin");
    await pageB.goto("/admin");

    // Logout via API direto (equivalente a clicar Sair no menu)
    await pageA.request.post("/api/auth/logout");

    // PageB navega e deve ser redirecionada pra /login (middleware detecta cookie ausente)
    await pageB.goto("/admin");
    await pageB.waitForURL(/\/login/, { timeout: 10_000 });
    expect(pageB.url()).toContain("/login");

    await context.close();
  });
});
