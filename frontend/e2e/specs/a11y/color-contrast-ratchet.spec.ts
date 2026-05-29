import { test, expect } from "../../fixtures/test-base";
import { runAxeStrict } from "../../utils/axe";

/**
 * F18 (Fase 5 test remediation) — ratchet de violacoes color-contrast.
 *
 * Estrategia: medir o ceiling atual de violacoes de color-contrast em
 * paginas-chave (publicas + autenticadas) e travar com `toBeLessThanOrEqual`.
 * Cada melhoria do tema MUI baixa o numero; quando chegar a 0, podemos
 * remover color-contrast da exclusao default em `axe.ts > runAxe`.
 *
 * Sem este spec, F18 ficava como "disabled rule + sem rastreamento" — o
 * pior dos mundos (regressao silenciosa, sem visibilidade).
 *
 * Ceiling INICIAL: capturado em primeira run. Ajustar a mao quando o tema
 * for refatorado. NUNCA AUMENTAR sem comentario explicito.
 */

const CEILING_LOGIN = 20;
const CEILING_CADASTRO_TREINADOR = 30;

test.describe("a11y: color-contrast ratchet (F18)", () => {
  test("login page — color-contrast violations dentro do ceiling", async ({ page }) => {
    await page.goto("/login");
    await expect(page.getByRole("heading").first()).toBeVisible();

    const result = await runAxeStrict(page);
    const colorContrast = result.violations.filter((v) => v.id === "color-contrast");
    const total = colorContrast.reduce((acc, v) => acc + v.nodes.length, 0);

    expect(total, `color-contrast violations em /login: ${total} (ceiling ${CEILING_LOGIN})`)
      .toBeLessThanOrEqual(CEILING_LOGIN);
  });

  test("cadastro/treinador — color-contrast violations dentro do ceiling", async ({ page }) => {
    await page.goto("/cadastro/treinador");
    await expect(page.getByRole("heading").first()).toBeVisible();

    const result = await runAxeStrict(page);
    const colorContrast = result.violations.filter((v) => v.id === "color-contrast");
    const total = colorContrast.reduce((acc, v) => acc + v.nodes.length, 0);

    expect(total, `color-contrast violations em /cadastro/treinador: ${total} (ceiling ${CEILING_CADASTRO_TREINADOR})`)
      .toBeLessThanOrEqual(CEILING_CADASTRO_TREINADOR);
  });
});
