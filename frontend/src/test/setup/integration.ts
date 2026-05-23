/**
 * Setup integration — env jsdom (vitest project "integration").
 *
 * Estende setup/unit com:
 * - jest-dom matchers (toBeInTheDocument, etc)
 * - Polyfills jsdom (matchMedia, IntersectionObserver, ResizeObserver)
 * - MSW server: intercepta TODO request HTTP nao mockado e falha o teste
 *
 * onUnhandledRequest: "error" eh deliberado. Forca testes a serem explicitos
 * sobre toda chamada de rede que fazem. Migracao gradual de vi.mock(axios) na Fase 5.
 *
 * Ate Fase 5 terminar, testes em src/test/* ainda mockam axios diretamente.
 * Suas requests nao chegam ao MSW (sao interceptadas pelo mock), entao MSW
 * onUnhandledRequest nao dispara para esses casos.
 */
import "@testing-library/jest-dom";
import "./unit";
import { afterAll, afterEach, beforeAll } from "vitest";
import { installGlobalMocks } from "./mocks";
import { server } from "../msw/server";

installGlobalMocks();

beforeAll(() => {
  server.listen({ onUnhandledRequest: "error" });
});

afterEach(() => {
  server.resetHandlers();
});

afterAll(() => {
  server.close();
});
