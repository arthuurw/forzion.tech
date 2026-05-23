/**
 * Polyfill matchMedia para jsdom (jsdom nao implementa por padrao).
 * Sem isso, MUI useMediaQuery e responsive hooks quebram em testes.
 */
export function installMatchMediaMock(): void {
  if (typeof window === "undefined") return;
  if (typeof window.matchMedia === "function") return;

  Object.defineProperty(window, "matchMedia", {
    writable: true,
    configurable: true,
    value: (query: string): MediaQueryList => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: () => {},
      removeListener: () => {},
      addEventListener: () => {},
      removeEventListener: () => {},
      dispatchEvent: () => false,
    }),
  });
}
