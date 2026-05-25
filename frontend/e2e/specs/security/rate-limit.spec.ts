import { test, expect } from "../../fixtures/test-base";

/**
 * Security 4/4 — rate limit em /api/auth (login).
 *
 * checkRateLimit (src/lib/rateLimit.ts): 10 req/min por IP. 11a request no
 * mesmo minuto retorna 429.
 *
 * Spec hammer-eia /api/auth com creds invalidas (sem efeito colateral) e
 * espera transition 401 -> 429 dentro de 11 attempts.
 */

test.describe("security: rate limit /api/auth", () => {
  test("11a tentativa retorna 429 Too Many Requests", async ({ request }) => {
    const attempts = 12;
    let saw429 = false;

    for (let i = 0; i < attempts; i++) {
      const response = await request.post("/api/auth", {
        data: { email: `rate-limit+${i}@e2e.test`, senha: "invalida" },
        failOnStatusCode: false,
      });
      if (response.status() === 429) {
        saw429 = true;
        expect(i).toBeGreaterThanOrEqual(10);
        break;
      }
    }

    expect(saw429, "esperava 429 dentro de 12 attempts").toBe(true);
  });
});
