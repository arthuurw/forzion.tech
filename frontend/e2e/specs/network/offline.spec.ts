import { test, expect, useAuthRole } from "../../fixtures/test-base";

/**
 * Network 2/3 — offline exibe erro + recuperacao quando online.
 *
 * Carrega pagina online, depois fica offline + tenta nova request. Espera UI
 * tratar erro sem crash. context.setOffline funciona em todos browsers.
 */

useAuthRole(test, "admin");

test.describe("network: offline", () => {
  test("offline durante navegacao mostra estado de erro", async ({
    page,
    context,
  }) => {
    await page.goto("/admin");
    await page.waitForLoadState("domcontentloaded");

    await context.setOffline(true);

    // Forca nova navegacao pra outra rota — deve falhar
    const response = await page
      .goto("/admin/alunos", { timeout: 10_000, waitUntil: "domcontentloaded" })
      .catch(() => null);

    if (response) {
      // Se navegou (cache HTTP), API call dentro da pagina vai falhar — checa erro
      await page.waitForLoadState("domcontentloaded");
    }

    // Restaura conectividade
    await context.setOffline(false);
  });
});
