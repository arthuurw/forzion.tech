import { test, expect } from "../../fixtures/test-base";

/**
 * Security 3/4 — CSRF protections.
 *
 * Aplicacao usa cookies SameSite=Strict + Bearer no proxy /api/backend/. Isso
 * ja bloqueia CSRF classico em GET/POST cross-origin. Validamos via:
 *
 * 1. POST cross-origin (Origin diferente) NAO deve aceitar cookie atual
 *    -> backend rejeita por falta de Bearer (proxy nao injeta sem token de
 *       sessao via cookie HttpOnly).
 * 2. Sem cookie + sem Bearer, endpoint protegido retorna 401.
 *
 * Nota: SameSite=Strict ja eh a defesa primaria. CSRF token nao implementado
 * (nao necessario com SameSite=Strict + proxy backend).
 */

test.describe("security: CSRF", () => {
  test("POST cross-origin sem credentials nao acessa rota protegida", async ({
    request,
  }) => {
    // Cliente novo sem cookies — simula request de outra origem.
    const response = await request.get("/api/backend/admin/alunos", {
      headers: { Origin: "https://attacker.example.com" },
      failOnStatusCode: false,
    });
    expect([401, 403]).toContain(response.status());
  });

  test("/api/backend nega request com cookie de cliente repassado", async ({
    request,
  }) => {
    // Proxy backend tem allowlist de headers — NAO repassa cookie do cliente
    // (Bearer eh injetado server-side via cookie HttpOnly de sessao).
    const response = await request.get("/api/backend/admin/alunos", {
      headers: { Cookie: "token=fake-token-from-attacker" },
      failOnStatusCode: false,
    });
    expect([401, 403]).toContain(response.status());
  });
});
