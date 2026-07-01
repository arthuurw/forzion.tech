import { test, expect, useAuthRole } from "../../fixtures/test-base";
import { AdminAlunosPage } from "../../pages/admin/AdminAlunosPage";
import { seedAluno, cleanupContaByEmail, makeTestEmail } from "../../utils/seed";

/**
 * Critical 2/8 — admin lista, filtra e gerencia alunos.
 *
 * Cria 1 aluno via API (seed), depois navega na UI admin pra listar + filtrar
 * + abrir detalhe. Cleanup via API no afterEach.
 */

useAuthRole(test, "admin");

let seededEmail: string | null = null;

test.describe("admin aluno CRUD", () => {
  test.beforeEach(async ({ request }) => {
    const treinadorId = process.env.E2E_TREINADOR_ID;
    const pacoteId = process.env.E2E_PACOTE_ID;
    expect(treinadorId).toBeTruthy();
    expect(pacoteId).toBeTruthy();

    seededEmail = makeTestEmail("crud-aluno");
    await seedAluno(request, {
      treinadorId: treinadorId!,
      pacoteId: pacoteId!,
      nome: "CRUD Test Aluno",
      email: seededEmail,
      senha: "Senha@123456",
      telefone: "11988887777",
    });
  });

  test.afterEach(async ({ request }) => {
    if (seededEmail) await cleanupContaByEmail(request, seededEmail);
    seededEmail = null;
  });

  test("lista carrega + filtro por nome retorna aluno seedado", async ({ page }) => {
    const alunos = new AdminAlunosPage(page);
    await alunos.goto();
    await expect(alunos.heading).toBeVisible();

    await alunos.filterByNome("CRUD Test");

    await expect(alunos.rows.first()).toBeVisible();
    const text = await alunos.rows.first().textContent();
    expect(text).toMatch(/CRUD Test/i);
  });

  test("filtro por status Inativo nao retorna aluno recem-criado", async ({ page }) => {
    const alunos = new AdminAlunosPage(page);
    await alunos.goto();

    // Aluno recem-criado vem como AguardandoAprovacao — filtrar Inativo deve
    // excluir ele.
    await alunos.statusFilter.click();
    await page.getByRole("option", { name: /inativo/i }).click();
    await alunos.filterByNome("CRUD Test");

    const matchCount = page
      .locator("tbody tr")
      .filter({ hasText: "CRUD Test" })
      ;
    await expect(matchCount).toHaveCount(0);
  });
});
