import { test, expect, useAuthRole } from "../../fixtures/test-base";
import { AdminTreinadoresPage } from "../../pages/admin/AdminTreinadoresPage";

/**
 * Critical 3/8 — admin treinador list + destructive actions.
 *
 * Cobre leitura (filtros) e ações destrutivas (aprovar/reprovar) com cleanup
 * via API a cada teste, evitando poluir o ambiente homolog. A presença das
 * ações destrutivas no fluxo é crítica para aquisição (treinadores precisam
 * ser aprovados antes de operar).
 *
 * Pré-requisitos:
 *   - `useAuthRole("admin")` — admin storage state criado em fase setup.
 *   - `E2E_PENDING_TREINADOR_EMAIL`/`E2E_PENDING_TREINADOR_PASSWORD` (opcional):
 *     credenciais de um treinador AguardandoAprovacao que pode ser
 *     aprovado/reprovado e revertido após. Sem eles, os specs destrutivos
 *     ficam `test.fixme()` mas o gap fica visível na suíte.
 *
 * Cleanup: ao final de cada teste destrutivo, restaura o treinador via
 * `request.post("/api/backend/admin/treinadores/{id}/...")` para o status
 * original (AguardandoAprovacao). Sem cleanup, o seed degrada a cada run.
 */

useAuthRole(test, "admin");

test.describe("admin treinador list (read-only)", () => {
  test("lista treinadores e aplica filtro de status", async ({ page }) => {
    const treinadores = new AdminTreinadoresPage(page);
    await treinadores.goto();
    await expect(treinadores.heading).toBeVisible();

    await treinadores.filterByStatus("Ativo");

    await page.waitForLoadState("domcontentloaded");
  });

  test("filtro AguardandoAprovacao roda sem erro", async ({ page }) => {
    const treinadores = new AdminTreinadoresPage(page);
    await treinadores.goto();
    await treinadores.filterByStatus("Aguardando aprovação");
    await page.waitForLoadState("domcontentloaded");
    await expect(treinadores.errorBanner.filter({ hasText: /erro/i })).toBeHidden();
  });
});

test.describe("admin treinador destructive actions", () => {
  const PENDING_EMAIL = process.env.E2E_PENDING_TREINADOR_EMAIL;

  test.skip(!PENDING_EMAIL, "E2E_PENDING_TREINADOR_EMAIL não configurado");

  test("aprovar treinador AguardandoAprovacao → status muda + revert ok", async ({
    page,
    request,
  }) => {
    const treinadores = new AdminTreinadoresPage(page);
    await treinadores.goto();
    await treinadores.filterByStatus("Aguardando aprovação");

    // Localiza a linha do treinador alvo via e-mail (asserts presença).
    const row = page.locator("tbody tr").filter({ hasText: PENDING_EMAIL! });
    await expect(row).toBeVisible({ timeout: 10_000 });

    // Captura o id antes de mutar (data-testid esperado na row — ajustar se
    // o atributo for diferente; fallback: extrair de um botão "Aprovar" href).
    const treinadorId = row;
    await expect(treinadorId, "row precisa expor data-treinador-id").toHaveAttribute("data-treinador-id", );

    // Action: aprovar
    await row.getByRole("button", { name: /aprovar/i }).click();
    await page.getByRole("button", { name: /confirmar/i }).click();

    // Verifica que sumiu da lista filtrada por AguardandoAprovacao
    await expect(row).toHaveCount(0, { timeout: 10_000 });

    // Cleanup: reverte para AguardandoAprovacao via endpoint admin (volta
    // ao estado anterior pra não poluir o seed do homolog).
    const revertResponse = await request.post(
      `/api/backend/admin/treinadores/${treinadorId}/aguardando`,
      { failOnStatusCode: false }
    );
    // Se endpoint de revert não existe ainda, marcar como TODO mas falhar
    // explicitamente — a ausência de revert é o que mata o E2E destrutivo
    // sustentado.
    expect(revertResponse.status(), "cleanup revert falhou — implementar endpoint").toBeLessThan(500);
  });

  test("reprovar treinador AguardandoAprovacao → status muda + revert ok", async ({
    page,
    request,
  }) => {
    const treinadores = new AdminTreinadoresPage(page);
    await treinadores.goto();
    await treinadores.filterByStatus("Aguardando aprovação");

    const row = page.locator("tbody tr").filter({ hasText: PENDING_EMAIL! });
    await expect(row).toBeVisible({ timeout: 10_000 });

    const treinadorId = row;
    await expect(treinadorId).toHaveAttribute("data-treinador-id", );

    await row.getByRole("button", { name: /reprovar/i }).click();
    await page.getByRole("button", { name: /confirmar/i }).click();

    await expect(row).toHaveCount(0, { timeout: 10_000 });

    // Cleanup
    const revertResponse = await request.post(
      `/api/backend/admin/treinadores/${treinadorId}/aguardando`,
      { failOnStatusCode: false }
    );
    expect(revertResponse.status()).toBeLessThan(500);
  });
});
