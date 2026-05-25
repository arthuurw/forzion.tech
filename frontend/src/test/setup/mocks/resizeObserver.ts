/**
 * Polyfill ResizeObserver para jsdom.
 * Usado por MUI Popper, Recharts, Floating UI.
 */
export function installResizeObserverMock(): void {
  if (typeof window === "undefined") return;
  if (typeof window.ResizeObserver === "function") return;

  class ResizeObserverMock implements ResizeObserver {
    constructor(_callback: ResizeObserverCallback) {}
    disconnect(): void {}
    observe(_target: Element, _options?: ResizeObserverOptions): void {}
    unobserve(_target: Element): void {}
  }

  Object.defineProperty(window, "ResizeObserver", {
    writable: true,
    configurable: true,
    value: ResizeObserverMock,
  });
  Object.defineProperty(globalThis, "ResizeObserver", {
    writable: true,
    configurable: true,
    value: ResizeObserverMock,
  });
}
