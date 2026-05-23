/**
 * Setup base — env node (vitest project "unit").
 *
 * Responsabilidades minimas (sem DOM):
 * - Limpar mocks entre testes
 * - Aplicar determinismo (random, UUID) - motion eh ignorado em node
 *
 * Polyfills DOM e jest-dom matchers ficam em setup/integration.ts.
 * Helpers de NextRequest ficam em setup/api.ts.
 */
import { afterEach, beforeEach, vi } from "vitest";
import { installDeterminism, uninstallDeterminism } from "../determinism";

beforeEach(() => {
  vi.clearAllMocks();
  installDeterminism();
});

afterEach(() => {
  uninstallDeterminism();
});
