import { test, expect } from "../../fixtures/test-base";
import { authStatePath, hasAuthState } from "../../fixtures/auth";
import { runAxe } from "../../utils/axe";

/**
 * A11y 1/1 — varre paginas-chave com @axe-core/playwright.
 *
 * WCAG 2.1 AA. Tag rules: wcag2a + wcag2aa + wcag21a + wcag21aa.
 * Regra `color-contrast` HABILITADA (F18 resolvido: tema MUI auditado/corrigido;
 * runAxe default agora gateia contraste — ver e2e/utils/axe.ts).
 *
 * Cada describe usa storage state por role (descope-scoped `test.use`).
 */

const PUBLIC_PAGES = ["/", "/login", "/cadastro/aluno", "/cadastro/treinador"];
const ADMIN_PAGES = ["/admin", "/admin/alunos", "/admin/treinadores"];
const ALUNO_PAGES = ["/aluno", "/aluno/fichas", "/aluno/assinatura"];
const TREINADOR_PAGES = ["/treinador", "/treinador/alunos"];

async function scanAxe(page: import("@playwright/test").Page, path: string) {
  await page.goto(path);
  await page.waitForLoadState("domcontentloaded");
  const results = await runAxe(page);
  expect(results.violations, JSON.stringify(results.violations, null, 2)).toEqual([]);
}

test.describe("a11y: paginas publicas", () => {
  for (const path of PUBLIC_PAGES) {
    test(`sem violacoes axe em ${path}`, async ({ page }) => {
      await scanAxe(page, path);
    });
  }
});

test.describe("a11y: admin", () => {
  test.use({ storageState: hasAuthState("admin") ? authStatePath("admin") : undefined });

  test.beforeAll(() => {
    if (!hasAuthState("admin")) {
      throw new Error("Storage state admin ausente — rode --project=setup");
    }
  });

  for (const path of ADMIN_PAGES) {
    test(`sem violacoes axe em ${path}`, async ({ page }) => {
      await scanAxe(page, path);
    });
  }
});

test.describe("a11y: aluno", () => {
  test.use({ storageState: hasAuthState("aluno") ? authStatePath("aluno") : undefined });

  test.beforeAll(() => {
    if (!hasAuthState("aluno")) {
      throw new Error("Storage state aluno ausente — rode --project=setup");
    }
  });

  for (const path of ALUNO_PAGES) {
    test(`sem violacoes axe em ${path}`, async ({ page }) => {
      await scanAxe(page, path);
    });
  }
});

test.describe("a11y: treinador", () => {
  test.use({ storageState: hasAuthState("treinador") ? authStatePath("treinador") : undefined });

  test.beforeAll(() => {
    if (!hasAuthState("treinador")) {
      throw new Error("Storage state treinador ausente — rode --project=setup");
    }
  });

  for (const path of TREINADOR_PAGES) {
    test(`sem violacoes axe em ${path}`, async ({ page }) => {
      await scanAxe(page, path);
    });
  }
});
