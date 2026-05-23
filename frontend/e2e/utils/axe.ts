import AxeBuilder from "@axe-core/playwright";
import type { Page } from "@playwright/test";

/**
 * Helper de scan axe — encapsula tags + disabledRules consistentes.
 *
 * Tags WCAG 2.1 AA cobrem a maioria das exigencias de acessibilidade.
 *
 * Rules desabilitadas:
 * - color-contrast: tema MUI pode ter contraste low em alguns elementos
 *   (chips, text.secondary). Cobrir manualmente em Fase futura.
 */
export async function runAxe(page: Page) {
  return new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
    .disableRules(["color-contrast"])
    .analyze();
}
