import { test, expect, useAuthRole } from "../../fixtures/test-base";

/**
 * Critical 6/8 — useInactivity dispara warn e logout.
 *
 * Hook usa setInterval (CHECK_MS=20s) + Date.now(). Em teste real seria 20min
 * de inatividade — inviavel. Estrategia: stub Date.now() no browser pra
 * "viajar no tempo" e forcar elapsed >= TIMEOUT_MS sem esperar.
 *
 * Constantes (src/hooks/useInactivity.ts):
 * - TIMEOUT_MS   = 30 * 60 * 1000 (logout)
 * - WARN_LEAD_MS =  5 * 60 * 1000 (aviso único 5 min antes do fim → aos 25 min)
 * - CHECK_MS     = 20 * 1000      (intervalo de check)
 */

useAuthRole(test, "admin");

test.describe("inactivity timeout", () => {
  test("avanca 25min de inatividade dispara Snackbar de warn", async ({ page }) => {
    await page.goto("/admin");
    await page.waitForLoadState("domcontentloaded");

    await page.evaluate(() => {
      const originalNow = Date.now.bind(Date);
      const start = originalNow();
      Date.now = () => start + 25 * 60 * 1000 + 1000;
    });

    // useInactivity ja registrou interval; aguarda proximo tick (CHECK_MS=20s).
    // Em vez de esperar 20s, ouvimos o Snackbar aparecer.
    const warnSnackbar = page.locator('[role="alert"]').filter({ hasText: /inatividade/i });
    await expect(warnSnackbar).toBeVisible({ timeout: 25_000 });
  });

  test("avanca 30min de inatividade redireciona pra /login", async ({ page }) => {
    await page.goto("/admin");
    await page.waitForLoadState("domcontentloaded");

    await page.evaluate(() => {
      const originalNow = Date.now.bind(Date);
      const start = originalNow();
      Date.now = () => start + 30 * 60 * 1000 + 1000;
    });

    await page.waitForURL(/\/login/, { timeout: 30_000 });
    expect(page.url()).toContain("/login");
  });
});
