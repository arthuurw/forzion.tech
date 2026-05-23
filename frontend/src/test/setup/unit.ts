/**
 * Setup unico do vitest.
 *
 * Responsabilidades:
 * - Importar matchers do @testing-library/jest-dom
 * - Instalar polyfills jsdom (matchMedia, IntersectionObserver, ResizeObserver)
 * - Aplicar determinismo (tempo, random, UUID, prefers-reduced-motion) em cada teste
 *
 * Arquivo apontado por vitest.config.mts -> test.setupFiles.
 */

import "@testing-library/jest-dom";
import { afterEach, beforeEach, vi } from "vitest";
import { installGlobalMocks } from "./mocks";
import { installDeterminism, uninstallDeterminism } from "../determinism";

installGlobalMocks();

beforeEach(() => {
  vi.clearAllMocks();
  installDeterminism();
});

afterEach(() => {
  uninstallDeterminism();
});
