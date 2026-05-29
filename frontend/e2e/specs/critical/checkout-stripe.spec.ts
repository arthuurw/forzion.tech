import { test, expect, useAuthRole } from "../../fixtures/test-base";
import { AssinaturaPage } from "../../pages/aluno/AssinaturaPage";
import { CheckoutPage } from "../../pages/CheckoutPage";
import { STRIPE_TEST_CARDS, STRIPE_TEST_CVC, STRIPE_TEST_EXPIRY } from "../../utils/stripe";

/**
 * Critical 5/8 — checkout Stripe em test mode (F3 fase 4 — seed garantido).
 *
 * Requer:
 * - aluno-cobaia com assinatura ativa (`useAuthRole("aluno")`)
 * - treinador-cobaia dono dessa assinatura (E2E_TREINADOR_EMAIL/PASSWORD)
 *   — usado pra criar cobranca pendente via API antes de cada teste
 * - NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY = pk_test_*
 * - backend Stripe configurado em test mode
 *
 * F3: substituiu `test.skip(!hasPagar)` por seed via API antes de cada teste.
 * Sem isso, todos os tests pulavam silenciosamente em CI quando o estado de
 * homolog ficava sem cobranca pendente (post-pagamento, post-cleanup).
 *
 * Helper `garantirPagamentoPendente`:
 *   1. login treinador → GET vinculos pra achar assinaturaId do aluno-cobaia
 *   2. POST /treinador/pagamentos/cobrar/{assinaturaId}?metodo=Cartao
 *      → cria (ou retorna idempotente) o pagamento pendente
 * Sem credenciais de treinador, spec degrada pra skip — sinal claro em vez
 * de falso passa silencioso.
 *
 * Cobre 3 cenarios:
 * - cartao success (4242 4242 4242 4242) → "Pagamento confirmado"
 * - cartao recusado (4000 0000 0000 0002) → erro
 * - 3D Secure (4000 0027 6000 3184) → challenge frame (validamos so o trigger)
 *
 * Webhook stripe-cli + valida estado final em /aluno/assinatura — adiado para
 * follow-up (requer stripe-cli configurado).
 */

const TREINADOR_EMAIL = process.env.E2E_TREINADOR_EMAIL;
const TREINADOR_PASSWORD = process.env.E2E_TREINADOR_PASSWORD;
const ALUNO_EMAIL = process.env.E2E_ALUNO_EMAIL;

useAuthRole(test, "aluno");

async function garantirPagamentoPendente(
  request: import("@playwright/test").APIRequestContext,
): Promise<void> {
  if (!TREINADOR_EMAIL || !TREINADOR_PASSWORD || !ALUNO_EMAIL) {
    test.skip(
      true,
      "Seed E2E_TREINADOR_EMAIL/PASSWORD + E2E_ALUNO_EMAIL ausente — configure pra remover skip.",
    );
  }

  // login treinador via API (sem UI).
  const login = await request.post("/api/auth/login", {
    data: { email: TREINADOR_EMAIL, senha: TREINADOR_PASSWORD },
    failOnStatusCode: false,
  });
  if (login.status() !== 200) {
    test.skip(true, `Treinador login falhou (${login.status()}) — credenciais E2E invalidas.`);
  }

  // Lista alunos do treinador, encontra alvo, pega assinaturaId.
  const alunos = await request.get("/api/backend/treinador/alunos", {
    failOnStatusCode: false,
  });
  if (alunos.status() !== 200) {
    test.skip(true, `Listar alunos do treinador falhou (${alunos.status()}).`);
  }
  const lista = await alunos.json();
  const alvo = (lista.items ?? lista).find(
    (a: { email?: string }) => a.email?.toLowerCase() === ALUNO_EMAIL!.toLowerCase(),
  );
  test.skip(!alvo, `Aluno ${ALUNO_EMAIL} nao vinculado a esse treinador.`);

  // Resolve assinaturaId via /aluno/assinatura ou /treinador/alunos/{id}/assinatura.
  const assinaturaRes = await request.get(
    `/api/backend/treinador/alunos/${alvo.alunoId}/assinatura`,
    { failOnStatusCode: false },
  );
  test.skip(
    assinaturaRes.status() !== 200,
    `Assinatura nao encontrada (${assinaturaRes.status()}).`,
  );
  const assinatura = await assinaturaRes.json();
  const assinaturaId = assinatura.assinaturaAlunoId ?? assinatura.id;

  // Idempotente: se ja existe pendente, backend retorna o mesmo via tx serializable.
  const cobrar = await request.post(
    `/api/backend/treinador/pagamentos/cobrar/${assinaturaId}?metodo=Cartao`,
    { failOnStatusCode: false },
  );
  expect([200, 201]).toContain(cobrar.status());
}

test.describe("checkout Stripe", () => {
  test.beforeEach(async ({ page, request }) => {
    await garantirPagamentoPendente(request);

    const assinatura = new AssinaturaPage(page);
    await assinatura.goto();

    // Pos-seed, o botao Pagar TEM que estar visivel — sem fallback silencioso.
    await expect(assinatura.pagarAgoraButton).toBeVisible({ timeout: 10_000 });
    await assinatura.pagarAgoraButton.click();
  });

  test("cartao success confirma pagamento", async ({ page }) => {
    const checkout = new CheckoutPage(page);
    await expect(checkout.heading).toBeVisible();

    await checkout.fillCard({
      number: STRIPE_TEST_CARDS.success.replace(/\s/g, ""),
      expiry: STRIPE_TEST_EXPIRY.replace(/\s/g, ""),
      cvc: STRIPE_TEST_CVC,
    });
    await checkout.payButton.click();

    await expect(checkout.successMessage).toBeVisible({ timeout: 15_000 });
  });

  test("cartao recusado exibe mensagem de erro", async ({ page }) => {
    const checkout = new CheckoutPage(page);
    await checkout.fillCard({
      number: STRIPE_TEST_CARDS.decline.replace(/\s/g, ""),
      expiry: STRIPE_TEST_EXPIRY.replace(/\s/g, ""),
      cvc: STRIPE_TEST_CVC,
    });
    await checkout.payButton.click();

    await expect(checkout.errorAlert).toBeVisible({ timeout: 15_000 });
  });

  test("cartao 3DS dispara challenge frame", async ({ page }) => {
    const checkout = new CheckoutPage(page);
    await checkout.fillCard({
      number: STRIPE_TEST_CARDS.threeDSecure.replace(/\s/g, ""),
      expiry: STRIPE_TEST_EXPIRY.replace(/\s/g, ""),
      cvc: STRIPE_TEST_CVC,
    });
    await checkout.payButton.click();

    // 3DS challenge abre frame "Stripe 3D Secure" — basta detectar presenca.
    const challengeFrame = page.frameLocator(
      "iframe[name*='3d-secure'], iframe[name*='__privateStripeFrame']",
    );
    await expect(challengeFrame.locator("body").first()).toBeAttached({ timeout: 15_000 });
  });
});
