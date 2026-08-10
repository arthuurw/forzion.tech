import { test, expect } from "../../fixtures/test-base";
import type { Page } from "../../fixtures/test-base";
import { authStatePath, hasAuthState } from "../../fixtures/auth";
import { seedConsent } from "../../utils/consent";

/**
 * A11y — checklist manual automatizavel: ordem de foco, foco visivel,
 * navegacao por teclado sem armadilha, reflow 320px, zoom 200%,
 * prefers-reduced-motion (A11Y-07, A11Y-08, A11Y-09).
 *
 * Complementa (nao substitui) o percurso manual com NVDA/VoiceOver
 * registrado em docs/accessibility-conformance.md — os criterios aqui sao
 * os automatizaveis por engine de browser; anuncio de leitor de tela exige
 * humano.
 */

const ROUTES: { label: string; path: string; role?: "aluno" | "treinador" }[] = [
  { label: "publica", path: "/" },
  { label: "aluno", path: "/aluno", role: "aluno" },
  { label: "treinador", path: "/treinador", role: "treinador" },
];

async function assertLogicalFocusOrder(page: Page, steps: number) {
  let prevTop = -Infinity;
  for (let i = 0; i < steps; i++) {
    await page.keyboard.press("Tab");
    const top = await page.evaluate(() => {
      const el = document.activeElement as HTMLElement | null;
      if (!el || el === document.body) return null;
      return el.getBoundingClientRect().top;
    });
    if (top === null) continue;
    expect(top, `Tab ${i + 1}: foco saltou para cima (ordem ilogica)`).toBeGreaterThanOrEqual(prevTop - 5);
    prevTop = top;
  }
}

async function assertVisibleFocus(page: Page) {
  await page.keyboard.press("Tab");
  await page.keyboard.press("Tab");
  const style = await page.evaluate(() => {
    const el = document.activeElement as HTMLElement | null;
    if (!el) return null;
    const cs = getComputedStyle(el);
    return { outlineStyle: cs.outlineStyle, outlineWidth: cs.outlineWidth, boxShadow: cs.boxShadow };
  });
  expect(style, "foco perdido apos Tab").not.toBeNull();
  const hasOutline = style!.outlineStyle !== "none" && style!.outlineWidth !== "0px";
  const hasBoxShadowRing = style!.boxShadow !== "none";
  expect(hasOutline || hasBoxShadowRing, `foco sem indicador visivel: ${JSON.stringify(style)}`).toBe(true);
}

async function overflowAtWidth(page: Page, path: string, width: number) {
  await page.setViewportSize({ width, height: 800 });
  await page.goto(path, { waitUntil: "domcontentloaded" });
  return page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
}

async function assertReducedMotionHonored(page: Page, path: string) {
  await page.goto(path, { waitUntil: "domcontentloaded" });
  const normal = await page.evaluate(() => {
    const el = document.querySelector<HTMLElement>(".skip-link");
    return el ? getComputedStyle(el).transitionDuration : null;
  });
  expect(normal, "skip-link ausente na pagina").not.toBeNull();

  await page.emulateMedia({ reducedMotion: "reduce" });
  const reduced = await page.evaluate(() => {
    const el = document.querySelector<HTMLElement>(".skip-link");
    return el ? getComputedStyle(el).transitionDuration : null;
  });
  expect(reduced, `prefers-reduced-motion nao neutralizou a transicao (${reduced})`).toBe("0.01ms");
  expect(reduced).not.toBe(normal);
}

for (const route of ROUTES) {
  test.describe(`a11y manual automatizavel: ${route.label} (${route.path})`, () => {
    if (route.role) {
      const role = route.role;
      test.use({ storageState: hasAuthState(role) ? authStatePath(role) : undefined });

      test.beforeAll(() => {
        if (!hasAuthState(role)) {
          throw new Error(`Storage state ${role} ausente — rode --project=setup`);
        }
      });
    }

    test.beforeEach(async ({ context }) => {
      await seedConsent(context);
    });

    test("ordem de foco logica (Tab sequencial sem salto)", async ({ page }) => {
      await page.goto(route.path, { waitUntil: "domcontentloaded" });
      await assertLogicalFocusOrder(page, 8);
    });

    test("foco visivel em elemento interativo", async ({ page }) => {
      await page.goto(route.path, { waitUntil: "domcontentloaded" });
      await assertVisibleFocus(page);
    });

    test("reflow a 320px sem overflow horizontal", async ({ page }) => {
      const overflow = await overflowAtWidth(page, route.path, 320);
      expect(overflow, `overflow horizontal a 320px: ${overflow}px`).toBeLessThanOrEqual(1);
    });

    test("zoom 200% sem perda de conteudo (viewport reduzido equivalente)", async ({ page }) => {
      const overflow = await overflowAtWidth(page, route.path, 640);
      expect(overflow, `overflow horizontal simulando zoom 200%: ${overflow}px`).toBeLessThanOrEqual(1);
    });

    test("prefers-reduced-motion honrado", async ({ page }) => {
      await assertReducedMotionHonored(page, route.path);
    });
  });
}

test.describe("a11y manual automatizavel: navegacao por teclado sem armadilha (dialog)", () => {
  test("ConsentBanner (auto-open em /): Tab contem o foco no dialog e Aceitar todos fecha via teclado", async ({
    page,
    context,
  }) => {
    await context.clearCookies();
    await page.goto("/", { waitUntil: "domcontentloaded" });

    const dialog = page.getByRole("dialog", { name: /cookies e privacidade/i });
    await expect(dialog).toBeVisible();

    for (let i = 0; i < 5; i++) {
      await page.keyboard.press("Tab");
      const containedInDialog = await page.evaluate(() => {
        const el = document.activeElement;
        const dlg = document.querySelector('[role="dialog"]');
        return !!dlg && !!el && dlg.contains(el);
      });
      expect(containedInDialog, `Tab ${i + 1}: foco escapou do dialog aberto para o conteudo de fundo`).toBe(
        true,
      );
    }

    await page.getByRole("button", { name: "Aceitar todos" }).focus();
    await page.keyboard.press("Enter");
    await expect(dialog, "dialog nao fechou via ativacao por teclado (Enter) — indicio de armadilha").toBeHidden();
  });

  test.describe("ConfirmDialog (/perfil, aluno)", () => {
    test.use({ storageState: hasAuthState("aluno") ? authStatePath("aluno") : undefined });

    test.beforeAll(() => {
      if (!hasAuthState("aluno")) {
        throw new Error("Storage state aluno ausente — rode --project=setup");
      }
    });

    test("Escape fecha o dialog e o foco retorna ao botao que abriu", async ({ page, context }) => {
      await seedConsent(context);
      await page.goto("/perfil", { waitUntil: "domcontentloaded" });

      const trigger = page.getByRole("button", { name: "Excluir minha conta" });
      await trigger.focus();
      await page.keyboard.press("Enter");

      const dialog = page.getByRole("dialog", { name: "Excluir minha conta" });
      await expect(dialog).toBeVisible();

      await page.keyboard.press("Escape");
      await expect(dialog).toBeHidden();

      const focusReturned = await trigger.evaluate((el) => el === document.activeElement);
      expect(focusReturned, "foco nao retornou ao botao trigger apos Escape — indicio de armadilha").toBe(true);
    });
  });
});
