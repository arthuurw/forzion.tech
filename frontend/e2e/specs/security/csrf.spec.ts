import { test, expect, useAuthRole } from "../../fixtures/test-base";

/**
 * Security 3/4 — defesas CSRF efetivas.
 *
 * Arquitetura defensiva:
 *   1. Cookie de sessão é HttpOnly + SameSite=Strict → browser não envia em
 *      requests cross-origin.
 *   2. Proxy `/api/backend/[...path]` tem allowlist `ALLOWED_REQUEST_HEADERS =
 *      ["content-type", "accept"]` — descarta `Cookie` e `Authorization` do
 *      cliente. Bearer só é injetado a partir do cookie HttpOnly lido server-side.
 *
 * Asserts EXPLÍCITOS (não `toBeOneOf([401, 403])`):
 *   - Sem credentials → 401 estrito.
 *   - Com `Cookie: token=evil` (proxy ignora) → 401 estrito.
 *   - Com `Authorization: Bearer evil` (proxy ignora) → 401 estrito.
 *   - **Positive control**: sessão autenticada real → mesmo endpoint ≠ 401.
 *     Isso distingue "defesa funciona" de "endpoint sempre 401".
 *
 * O ataque CSRF clássico (form submit cross-origin no browser da vítima) já
 * é bloqueado por SameSite=Strict; este spec valida a defesa em depth do
 * proxy ao simular vetores onde o atacante já consegue forjar o request.
 */

const ADMIN_ALUNOS = "/api/backend/admin/alunos";

test.describe("security: CSRF — endpoint protegido sem credentials válidas", () => {
  test("sem cookie e sem Bearer → 401", async ({ request }) => {
    const response = await request.get(ADMIN_ALUNOS, {
      headers: { Origin: "https://attacker.example.com" },
      failOnStatusCode: false,
    });
    expect(response.status()).toBe(401);
  });

  test("Cookie forjado do cliente NÃO é repassado pelo proxy → 401", async ({
    request,
  }) => {
    // Proxy `/api/backend/[...path]` tem allowlist e dropa `Cookie`. Mesmo que o
    // atacante consiga injetar um cookie, o backend não recebe.
    const response = await request.get(ADMIN_ALUNOS, {
      headers: { Cookie: "token=fake-jwt-from-attacker" },
      failOnStatusCode: false,
    });
    expect(response.status()).toBe(401);
  });

  test("Authorization header forjado NÃO é repassado pelo proxy → 401", async ({
    request,
  }) => {
    // ALLOWED_REQUEST_HEADERS exclui `authorization` — proxy só injeta Bearer
    // a partir do cookie HttpOnly lido server-side.
    const response = await request.get(ADMIN_ALUNOS, {
      headers: {
        Authorization: "Bearer fake-jwt-from-attacker",
        Origin: "https://attacker.example.com",
      },
      failOnStatusCode: false,
    });
    expect(response.status()).toBe(401);
  });

  test("POST cross-origin sem cookie de sessão real → 401 (sem 200)", async ({
    request,
  }) => {
    const response = await request.post(ADMIN_ALUNOS, {
      headers: {
        Origin: "https://attacker.example.com",
        "Content-Type": "application/json",
      },
      data: { nome: "csrf-injected" },
      failOnStatusCode: false,
    });
    expect(response.status()).toBe(401);
  });
});

test.describe("security: CSRF — positive control (sessão admin válida)", () => {
  useAuthRole(test, "admin");

  test("sessão real consegue acessar o mesmo endpoint (status ≠ 401)", async ({
    request,
  }) => {
    // Mesma URL dos asserts negativos acima — com cookie HttpOnly real, o proxy
    // injeta Bearer e o backend autentica. Se este teste falhar com 401, a
    // bateria negativa estaria passando por motivo errado (endpoint sempre nega)
    // e não por causa das defesas CSRF.
    const response = await request.get(ADMIN_ALUNOS, { failOnStatusCode: false });
    expect(response.status()).not.toBe(401);
    expect(response.status()).not.toBe(403);
    expect(response.status()).toBeLessThan(500);
  });
});
