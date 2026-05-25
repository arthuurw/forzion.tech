import { test, expect } from "../../fixtures/test-base";

/**
 * Security 2/4 — cookies de auth tem HttpOnly + Secure (prod) + SameSite=strict.
 *
 * Setados pelo handler /api/auth/route.ts apos login bem-sucedido. Tests
 * fazem login via API request e inspecionam cookies via context.cookies().
 *
 * Em dev (NODE_ENV !== production), Secure=false. Spec eh tolerante: se
 * baseURL eh https://, exige Secure=true; senao, aceita ambos.
 */

test.describe("security: auth cookies", () => {
  test("cookies token + session_guard tem flags HttpOnly + SameSite=strict", async ({
    request,
    context,
    baseURL,
  }) => {
    const email = process.env.E2E_ADMIN_EMAIL;
    const password = process.env.E2E_ADMIN_PASSWORD;
    expect(email).toBeTruthy();
    expect(password).toBeTruthy();

    const response = await request.post("/api/auth", {
      data: { email, senha: password },
    });
    expect(response.ok()).toBeTruthy();

    const cookies = await context.cookies();
    const token = cookies.find((c) => c.name === "token");
    const guard = cookies.find((c) => c.name === "session_guard");

    expect(token, "cookie 'token' setado").toBeDefined();
    expect(guard, "cookie 'session_guard' setado").toBeDefined();

    for (const c of [token!, guard!]) {
      expect(c.httpOnly, `cookie ${c.name} deve ser HttpOnly`).toBe(true);
      expect(c.sameSite, `cookie ${c.name} deve ser SameSite=Strict`).toBe("Strict");
      if (baseURL?.startsWith("https://")) {
        expect(c.secure, `cookie ${c.name} deve ser Secure em https`).toBe(true);
      }
      expect(c.path, `cookie ${c.name} path=/`).toBe("/");
    }
  });
});
