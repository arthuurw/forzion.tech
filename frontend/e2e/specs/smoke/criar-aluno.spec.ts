import { test, expect, useAuthRole } from "../../fixtures/test-base";
import { seedAluno, cleanupContaByEmail, makeTestEmail, makeTestSenha } from "../../utils/seed";

/**
 * Smoke 4/5 — cria aluno via API e cleanup como admin.
 *
 * Estrategia API-only (sem UI) pra smoke rapido. Specs criticos (Fase 10a
 * critical/) exercitam o fluxo UI completo.
 *
 * Requer:
 * - E2E_TREINADOR_ID / E2E_PACOTE_ID (treinador ativo + pacote dele)
 * - admin storage state (pra cleanup)
 */

useAuthRole(test, "admin");

test.describe("smoke @smoke", () => {
  test("cria aluno via API + cleanup", async ({ request }) => {
    const treinadorId = process.env.E2E_TREINADOR_ID;
    const pacoteId = process.env.E2E_PACOTE_ID;

    expect(treinadorId, "E2E_TREINADOR_ID ausente").toBeTruthy();
    expect(pacoteId, "E2E_PACOTE_ID ausente").toBeTruthy();

    const email = makeTestEmail("smoke-aluno");

    const created = await seedAluno(request, {
      treinadorId: treinadorId!,
      pacoteId: pacoteId!,
      nome: "Smoke Test Aluno",
      email,
      senha: makeTestSenha(),
      telefone: "11999990000",
      diasDisponiveis: 3,
      tempoDisponivelMinutos: 60,
      finalidade: "Hipertrofia",
      nivelCondicionamento: "Iniciante",
    });

    expect(created.email).toBe(email);

    await cleanupContaByEmail(request, email);
  });
});
