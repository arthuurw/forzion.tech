import { test, expect } from "../../fixtures/test-base";
import { LoginPage } from "../../pages/LoginPage";

/**
 * Critical 9/8+ — logout adiciona o JTI à lista de revogados e o middleware
 * rejeita o JWT antigo em qualquer endpoint protegido.
 *
 * Sem este teste, a presença da entidade `TokenRevogado` + repo + handler dá
 * só confiança de unidade — não prova que o JwtMiddleware realmente consulta
 * a lista. Cobertura unit: TokenRevogadoTests / LogoutHandlerTests.
 *
 * Estratégia:
 *   1. Login real e captura o cookie `token` (HttpOnly via cookies do contexto).
 *   2. Chama `/api/auth/logout` (proxy revoga via backend).
 *   3. Cria um novo contexto SEM cookies + tenta acessar `/api/backend/conta/perfil`
 *      enviando o cookie `token` antigo manualmente via `request.get` —
 *      o backend deve responder 401.
 *
 * Pré-requisito: env `E2E_ADMIN_EMAIL` + `E2E_ADMIN_PASSWORD` para login.
 */

test.describe("auth: logout revokes JWT", () => {
  test("JWT antigo rejeitado após logout", async ({ page, context, baseURL }) => {
    const email = process.env.E2E_ADMIN_EMAIL;
    const password = process.env.E2E_ADMIN_PASSWORD;
    expect(email, "E2E_ADMIN_EMAIL não configurado").toBeTruthy();
    expect(password, "E2E_ADMIN_PASSWORD não configurado").toBeTruthy();

    const login = new LoginPage(page);
    await login.goto();
    await login.submit(email!, password!);
    await page.waitForURL(/\/admin/);

    // Captura o cookie `token` (HttpOnly, mas visível via context.cookies())
    const cookies = await context.cookies();
    const tokenCookie = cookies.find((c) => c.name === "token");
    expect(tokenCookie, "cookie token deveria existir após login").toBeDefined();

    // Sanity: endpoint protegido funciona com o cookie atual
    const okResponse = await page.request.get("/api/backend/conta/perfil");
    expect(okResponse.status(), "perfil deveria carregar antes do logout").toBeLessThan(400);

    // 2. Logout (proxy revoga JTI no backend)
    await page.request.post("/api/auth/logout");

    // 3. Tenta reusar o cookie antigo com um contexto novo (sem session_guard)
    const replayCtx = await context.browser()!.newContext();
    await replayCtx.addCookies([
      {
        ...tokenCookie!,
        // garante que estamos enviando exatamente o token capturado
        name: "token",
        value: tokenCookie!.value,
      },
    ]);
    const replayResponse = await replayCtx.request.get(
      `${baseURL ?? ""}/api/backend/conta/perfil`,
      { failOnStatusCode: false }
    );
    // Se a defesa funciona: 401. Se NÃO funciona, perfil carrega (200) — vazamento.
    expect(replayResponse.status(), "JWT revogado deveria retornar 401").toBe(401);

    await replayCtx.close();
  });
});
