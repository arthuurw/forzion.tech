import { test, expect, useAuthRole } from "../../fixtures/test-base";

/**
 * F29 (Fase 4 test remediation) — Stripe Connect onboarding E2E.
 *
 * Cobre o fluxo de configuracao Stripe Express do treinador:
 *   1. Treinador aprovado entra em /treinador/pagamentos.
 *   2. Status inicial: "Configurar recebimentos" (onboardingCompleto=false).
 *   3. Click → POST /treinador/onboarding → backend cria conta Stripe Connect
 *      + gera link de onboarding → frontend redireciona pra window.location.href.
 *   4. Retorno (mock OU real): GET /treinador/onboarding/status → contaConfigurada=true.
 *
 * Em ambientes E2E reais (homolog) com Stripe test mode, o redirect leva pra
 * Stripe.com. Aqui validamos APENAS a tela do nosso app — o redirect e a
 * tela de retorno (`/treinador/onboarding/retorno`).
 *
 * O fluxo COMPLETO (incluindo Stripe Express form) precisa de Stripe-cli
 * stub ou hook de simulacao — fora do scope deste spec. Marcado test.fixme
 * pra deixar trilha pra Fase 5.
 */

test.describe("critical: Stripe Connect onboarding (treinador)", () => {
  useAuthRole(test, "treinador");

  test("treinador sem onboarding completo vê 'Configurar recebimentos'", async ({ page }) => {
    await page.goto("/treinador/pagamentos");

    // Loading spinner aparece e some.
    await expect(page.getByRole("progressbar")).toBeHidden({ timeout: 10_000 });

    // Estado depende do treinador-cobaia: ou "Ativo" (onboarding completo),
    // ou "Configurar recebimentos" (sem). Aceita os dois — o que NAO pode e
    // erro silencioso.
    const button = page.getByRole("button", { name: /configurar recebimentos|continuar cadastro/i });
    const ativo = page.getByText(/^ativo$/i);

    const atual = await Promise.race([
      button.waitFor({ state: "visible", timeout: 5_000 }).then(() => "configurar"),
      ativo.waitFor({ state: "visible", timeout: 5_000 }).then(() => "ativo"),
    ]).catch(() => null);

    expect(atual, "estado da pagina /treinador/pagamentos deve estar resolvido (Ativo ou Configurar)").toBeTruthy();
  });

  test("clique em 'Configurar recebimentos' dispara POST /treinador/onboarding", async ({ page }) => {
    await page.goto("/treinador/pagamentos");
    await expect(page.getByRole("progressbar")).toBeHidden({ timeout: 10_000 });

    const btn = page.getByRole("button", { name: /configurar recebimentos/i });
    const visible = await btn.isVisible().catch(() => false);
    test.skip(!visible, "Treinador-cobaia ja tem onboarding completo — re-skip ate reset.");

    // Captura a request via route — confirma payload + reescreve resposta
    // pra evitar redirect real pro Stripe (que sai do dominio do app).
    let onboardingCalled = false;
    await page.route("**/treinador/onboarding", async (route, request) => {
      if (request.method() === "POST") {
        onboardingCalled = true;
        const body = request.postDataJSON() as { urlRetorno?: string; urlCancelamento?: string };
        expect(body.urlRetorno).toContain("/treinador/onboarding/retorno");
        await route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify({ url: "/treinador/pagamentos?stripe=mocked" }),
        });
      } else {
        await route.continue();
      }
    });

    await btn.click();
    // Aguarda o app processar o response.
    await expect.poll(() => onboardingCalled, { timeout: 10_000 }).toBe(true);
  });

  test("pagina de retorno /treinador/onboarding/retorno consulta status", async ({ page }) => {
    let statusChecked = false;
    await page.route("**/treinador/onboarding/status", async (route) => {
      statusChecked = true;
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ onboardingCompleto: true, contaConfigurada: true }),
      });
    });

    await page.goto("/treinador/onboarding/retorno");

    await expect(page.getByText("Cadastro concluído!")).toBeVisible({ timeout: 10_000 });
    expect(statusChecked).toBe(true);
  });

  // eslint-disable-next-line playwright/expect-expect
  test.fixme(
    "fluxo end-to-end Stripe Express (form externo) — precisa Stripe-cli stub",
    async () => {}
  );
});
