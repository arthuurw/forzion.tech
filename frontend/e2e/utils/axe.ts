import AxeBuilder from "@axe-core/playwright";
import type { Page } from "@playwright/test";

/**
 * Helper de scan axe — encapsula tags WCAG consistentes.
 *
 * Tags WCAG 2.1 AA cobrem a maioria das exigencias de acessibilidade,
 * INCLUINDO `color-contrast` (1.4.3).
 *
 * F18 RESOLVIDO: o tema MUI foi auditado/corrigido (text.secondary #4B5563 ≈ 7.1:1,
 * error.main #C62828 ≈ 5.3:1 em ambos os fundos do app) e `color-contrast` agora
 * GATEIA por padrao neste helper — sem ratchet, sem disableRules. Ver
 * specs/specification-frontend-ui.md (§acessibilidade).
 */
export async function runAxe(page: Page) {
  return new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
    .analyze();
}
