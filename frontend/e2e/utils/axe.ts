import AxeBuilder from "@axe-core/playwright";
import type { Page } from "@playwright/test";

/**
 * Helper de scan axe — encapsula tags + disabledRules consistentes.
 *
 * Tags WCAG 2.1 AA cobrem a maioria das exigencias de acessibilidade.
 *
 * Rules desabilitadas (legacy):
 * - color-contrast: tema MUI tem casos de baixo contraste (chips, text.secondary).
 *   F18 (Fase 5): runAxeStrict() inclui essa rule pra tracker explicito; uso
 *   sob ratchet, nao gate hard ainda. Auditoria de tema fica out-of-scope.
 */
export async function runAxe(page: Page) {
  return new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
    .disableRules(["color-contrast"])
    .analyze();
}

/**
 * F18 — variante STRICT que inclui color-contrast pra rastrear violations sem
 * bloquear (use junto com `expect(violations.length).toBeLessThanOrEqual(N)`).
 * Quando tema MUI for ajustado, ratcheta N pra baixo ate 0; ai pode mover
 * color-contrast pra `runAxe` default.
 *
 * Uso esperado: 1 spec dedicado em e2e/specs/a11y/ que mede o ceiling de
 * violacoes e falha SE ultrapassar.
 */
export async function runAxeStrict(page: Page) {
  return new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
    .analyze();
}
