import type { Page } from "@playwright/test";

/**
 * Helpers de memory leak detection. Usados em Fase 11 (memory leak specs).
 *
 * `measureHeap()` so funciona em Chromium com `--js-flags=--expose-gc`. Em
 * outros browsers retorna null. Specs devem skip-ar quando null.
 */

export interface HeapSnapshot {
  usedJSHeapSize: number;
  totalJSHeapSize: number;
  jsHeapSizeLimit: number;
}

export async function measureHeap(page: Page): Promise<HeapSnapshot | null> {
  return page.evaluate(() => {
    const perf = performance as Performance & { memory?: HeapSnapshot };
    if (!perf.memory) return null;
    return {
      usedJSHeapSize: perf.memory.usedJSHeapSize,
      totalJSHeapSize: perf.memory.totalJSHeapSize,
      jsHeapSizeLimit: perf.memory.jsHeapSizeLimit,
    };
  });
}

/**
 * Forca GC se exposto via --expose-gc. Sem isso, mediCoes de heap incluem lixo
 * nao coletado. Retorna true se rodou; false caso contrario.
 */
export async function forceGC(page: Page): Promise<boolean> {
  return page.evaluate(() => {
    const w = window as Window & { gc?: () => void };
    if (typeof w.gc !== "function") return false;
    w.gc();
    return true;
  });
}
