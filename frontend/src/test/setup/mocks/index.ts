import { installMatchMediaMock } from "./matchMedia";
import { installIntersectionObserverMock } from "./intersectionObserver";
import { installResizeObserverMock } from "./resizeObserver";
import { installBlobStreamPolyfill } from "./blobStream";

/**
 * Instala todos os polyfills globais que o jsdom nao fornece.
 * Chamado uma vez no setup do vitest.
 */
export function installGlobalMocks(): void {
  installMatchMediaMock();
  installIntersectionObserverMock();
  installResizeObserverMock();
  installBlobStreamPolyfill();
}

export { installMatchMediaMock } from "./matchMedia";
export { installIntersectionObserverMock } from "./intersectionObserver";
export { installResizeObserverMock } from "./resizeObserver";
export { installBlobStreamPolyfill } from "./blobStream";
