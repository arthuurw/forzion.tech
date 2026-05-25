import { test, expect, useAuthRole } from "../../fixtures/test-base";

/**
 * Network 1/3 — slow3G nao quebra navegacao admin (carrega eventualmente).
 *
 * CDP only chromium. Skip em firefox/webkit.
 */

useAuthRole(test, "admin");

test.describe("network: slow3G", () => {
  test("admin abre /admin/alunos sob slow3G e finaliza load", async ({
    page,
    network,
    browserName,
  }) => {
    test.skip(browserName !== "chromium", "CDP throttle so funciona em Chromium");

    await network.slow3G();

    // Timeout amplo: slow3G estima ~500Kbps + 400ms latency. Bundle Next pode
    // levar 10-20s. Eleva timeout pra 60s.
    await page.goto("/admin/alunos", { timeout: 60_000, waitUntil: "domcontentloaded" });

    await expect(page.getByRole("heading", { name: /^alunos$/i })).toBeVisible({
      timeout: 60_000,
    });
  });
});
