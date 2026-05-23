import { test as base, expect } from "@playwright/test";
import type { Page } from "@playwright/test";
import {
  applyNetwork,
  goOffline,
  goOnline,
  flakyRoute,
  NETWORK_PRESETS,
  type NetworkConditions,
} from "./network";
import { authStatePath, hasAuthState, type AuthRole } from "./auth";

/**
 * Test base custom — fixtures compartilhadas pelos specs.
 *
 * Uso:
 *   import { test, expect } from "../fixtures/test-base";
 *
 *   test("X", async ({ page, network }) => {
 *     await network.slow3G();
 *     await page.goto("/");
 *   });
 */

interface NetworkFixture {
  apply: (conditions: NetworkConditions) => Promise<void>;
  slow3G: () => Promise<void>;
  fast3G: () => Promise<void>;
  offline: () => Promise<void>;
  online: () => Promise<void>;
  flaky: (
    pattern: string | RegExp,
    failCount: number,
    failStatus?: number,
  ) => ReturnType<typeof flakyRoute>;
}

interface ConsoleErrorsFixture {
  errors: string[];
  assertNoErrors: () => void;
}

export const test = base.extend<{
  network: NetworkFixture;
  consoleErrors: ConsoleErrorsFixture;
}>({
  network: async ({ context, page }, use) => {
    const fixture: NetworkFixture = {
      apply: (conditions) => applyNetwork(context, page, conditions),
      slow3G: () => applyNetwork(context, page, NETWORK_PRESETS.slow3G),
      fast3G: () => applyNetwork(context, page, NETWORK_PRESETS.fast3G),
      offline: () => goOffline(context, page),
      online: () => goOnline(context, page),
      flaky: (pattern, failCount, failStatus) => flakyRoute(page, pattern, failCount, failStatus),
    };
    await use(fixture);
    // Reseta para estado online ao fim do teste (best-effort).
    await context.setOffline(false).catch(() => undefined);
  },

  consoleErrors: async ({ page }, use) => {
    const errors: string[] = [];
    const handler = (msg: import("@playwright/test").ConsoleMessage) => {
      if (msg.type() === "error") {
        errors.push(msg.text());
      }
    };
    page.on("console", handler);

    const fixture: ConsoleErrorsFixture = {
      errors,
      assertNoErrors: () => {
        expect(errors, `console.error capturado durante o teste`).toEqual([]);
      },
    };

    await use(fixture);
    page.off("console", handler);
  },
});

export { expect };

/**
 * Helper pra opt-in numa role autenticada em um spec inteiro:
 *
 *   useAuthRole(test, "admin");
 *
 * Equivalente direto a `test.use({ storageState: authStatePath(role) })`.
 * Fase 10a: estrategia fail loud — se o storage state nao existir, Playwright
 * erra ao tentar carregar contexto. Mensagem clara via guard explicito.
 */
export function useAuthRole(t: typeof test, role: AuthRole): void {
  if (!hasAuthState(role)) {
    throw new Error(
      `Storage state ausente pra role ${role}. Configure E2E_${role.toUpperCase()}_EMAIL ` +
        `e E2E_${role.toUpperCase()}_PASSWORD e rode "npx playwright test --project=setup".`,
    );
  }
  t.use({ storageState: authStatePath(role) });
}

export type { AuthRole, Page };
