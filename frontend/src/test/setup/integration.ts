/**
 * Setup integration — env jsdom (vitest project "integration").
 *
 * Estende setup/unit com:
 * - jest-dom matchers (toBeInTheDocument, etc)
 * - Polyfills jsdom (matchMedia, IntersectionObserver, ResizeObserver)
 *
 * MSW server.listen() sera adicionado aqui na Fase 3.
 */
import "@testing-library/jest-dom";
import "./unit";
import { installGlobalMocks } from "./mocks";

installGlobalMocks();
