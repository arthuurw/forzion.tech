import { test, expect, useAuthRole } from "../../fixtures/test-base";

/**
 * F28 (Fase 4 test remediation) — role revogada mid-flight.
 *
 * Cenario: treinador autenticado emite token JWT valido. Admin inativa o
 * treinador (via API admin). Proxima request do treinador com JWT VALIDO
 * (assinatura ok, nao expirado, nao revogado individualmente) deve receber
 * 403 — o backend re-resolve role do banco em cada request via
 * AuthorizationFilter/PerfilIdRequiredFilter, NAO confia so no claim do JWT.
 *
 * Defesa em depth: token valido + role revogada → bloqueio. Sem isso, o
 * treinador inativado continuaria operando ate o JWT expirar (~24h padrao).
 *
 * Skip por env: E2E_TREINADOR_TEST_EMAIL / E2E_ADMIN_EMAIL precisam ser
 * setados pra um treinador-cobaia. Spec skipa silenciosamente em ambientes
 * sem essa infraestrutura — defer pra CI homolog.
 */

const TREINADOR_EMAIL = process.env.E2E_TREINADOR_REVOKE_EMAIL;
const TREINADOR_PASSWORD = process.env.E2E_TREINADOR_REVOKE_PASSWORD;

test.describe("security: role revoked mid-request", () => {
  test.skip(
    !TREINADOR_EMAIL || !TREINADOR_PASSWORD,
    "Set E2E_TREINADOR_REVOKE_EMAIL + E2E_TREINADOR_REVOKE_PASSWORD pra rodar (treinador-cobaia que pode ser inativado/reativado).",
  );

  test("treinador inativado pelo admin → proxima request com JWT valido → 403", async ({
    browser,
    request,
  }) => {
    // ETAPA 1 — Treinador logado e con JWT util. Usa contexto isolado pra
    // capturar o cookie HttpOnly do proxy.
    const treinadorContext = await browser.newContext();
    const treinadorPage = await treinadorContext.newPage();
    await treinadorPage.goto("/login");
    await treinadorPage.getByLabel(/e-?mail/i).fill(TREINADOR_EMAIL!);
    await treinadorPage.getByLabel(/senha/i).fill(TREINADOR_PASSWORD!);
    await treinadorPage.getByRole("button", { name: /entrar|login/i }).click();
    await treinadorPage.waitForURL(/\/treinador/);

    // Sanity: GET /treinador/me ou similar funciona com 200.
    const sanity = await treinadorPage.request.get("/api/backend/treinador/alunos", {
      failOnStatusCode: false,
    });
    expect(sanity.status()).toBeLessThan(400);

    // ETAPA 2 — Admin inativa esse treinador. Precisa de admin context separado.
    const adminContext = await browser.newContext({
      storageState: process.env.E2E_ADMIN_STORAGE_STATE ?? undefined,
    });
    const adminPage = await adminContext.newPage();

    // Busca o treinadorId via API admin (lista treinadores por email-filtro).
    const treinadores = await adminPage.request.get("/api/backend/admin/treinadores", {
      failOnStatusCode: false,
    });
    if (treinadores.status() === 401) {
      test.skip(true, "Admin sem auth state — set E2E_ADMIN_STORAGE_STATE.");
    }
    const lista = await treinadores.json();
    const alvo = (lista.items ?? lista).find(
      (t: { email?: string }) => t.email?.toLowerCase() === TREINADOR_EMAIL!.toLowerCase(),
    );
    test.skip(!alvo, `Treinador ${TREINADOR_EMAIL} nao encontrado na listagem admin.`);

    const inativarRes = await adminPage.request.post(
      `/api/backend/admin/treinadores/${alvo.treinadorId}/inativar`,
      { data: { observacao: "F28 E2E role-revoked test" }, failOnStatusCode: false },
    );
    expect(inativarRes.status()).toBe(200);

    // ETAPA 3 — Mesmo treinador (mesmo JWT valido!) tenta request → 403.
    const blocked = await treinadorPage.request.get("/api/backend/treinador/alunos", {
      failOnStatusCode: false,
    });
    expect([401, 403]).toContain(blocked.status());

    // ETAPA 4 — Cleanup: reativa treinador pra nao ficar inutilizavel em CI.
    // POST pra endpoint reverso (se existir) ou via admin: dependendo da API,
    // pode precisar de re-aprovacao. Best-effort.
    await adminPage.request.post(
      `/api/backend/admin/treinadores/${alvo.treinadorId}/aprovar`,
      { data: { observacao: "Reverter F28 test" }, failOnStatusCode: false },
    );

    await treinadorContext.close();
    await adminContext.close();
  });
});

/**
 * Variante "true mid-request": usar route delay + paralela. Skip por default
 * por ser flaky em CI shared. Inclui pra documentar a tecnica.
 */
test.describe("security: role revoked DURANTE request in-flight (skip)", () => {
  test.fixme(
    true,
    "Requires deterministic request delay hook in backend (E2E_REQUEST_DELAY_MS). Tracked separately.",
  );

  useAuthRole(test, "admin");

  test("admin inativa treinador enquanto treinador faz request lenta → 403", async () => {
    // Stub: precisa de um endpoint backend que aceita ?delay=Nms (apenas em
    // env Test). Sem isso, race condition real e dificil de forcar em E2E.
  });
});
