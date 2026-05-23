/**
 * Polyfill IntersectionObserver para jsdom.
 * Usado por libs de lazy-load, virtualization, infinite-scroll.
 */
export function installIntersectionObserverMock(): void {
  if (typeof window === "undefined") return;
  if (typeof window.IntersectionObserver === "function") return;

  class IntersectionObserverMock implements IntersectionObserver {
    readonly root: Element | Document | null = null;
    readonly rootMargin: string = "";
    readonly thresholds: ReadonlyArray<number> = [];

    constructor(_callback: IntersectionObserverCallback, _options?: IntersectionObserverInit) {}
    disconnect(): void {}
    observe(_target: Element): void {}
    takeRecords(): IntersectionObserverEntry[] { return []; }
    unobserve(_target: Element): void {}
  }

  Object.defineProperty(window, "IntersectionObserver", {
    writable: true,
    configurable: true,
    value: IntersectionObserverMock,
  });
  Object.defineProperty(globalThis, "IntersectionObserver", {
    writable: true,
    configurable: true,
    value: IntersectionObserverMock,
  });
}
