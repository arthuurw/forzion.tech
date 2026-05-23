import { test, expect, useAuthRole } from "../../fixtures/test-base";
import { measureHeap, forceGC } from "../../utils/memory";

/**
 * Memory leak 1/1 — navegacao repetida entre rotas admin nao deve vazar heap.
 *
 * Padrao:
 * 1. Baseline: navegar 2x (warmup), forceGC, measureHeap
 * 2. Stress: navegar 10x mais (alunos <-> treinadores)
 * 3. Final: forceGC, measureHeap
 * 4. Assert: final <= baseline * 1.5
 *
 * Requer Chromium com `--js-flags=--expose-gc`. Em outros browsers
 * (performance.memory + window.gc indisponiveis), spec eh skip-ado.
 *
 * Threshold 1.5x eh conservador per plano §12 ("Memory leak false positive:
 * --expose-gc no Chromium; threshold 1.5x conservador").
 */

useAuthRole(test, "admin");

test.describe("memory: navigation leak", () => {
  test("alternar alunos<->treinadores 10x nao cresce heap > 1.5x", async ({
    page,
    browserName,
  }) => {
    test.skip(browserName !== "chromium", "performance.memory + window.gc so em Chromium");

    // Warmup
    await page.goto("/admin/alunos");
    await page.waitForLoadState("domcontentloaded");
    await page.goto("/admin/treinadores");
    await page.waitForLoadState("domcontentloaded");

    const gcAvailable = await forceGC(page);
    test.skip(
      !gcAvailable,
      "window.gc nao exposto — rode Chromium com --js-flags=--expose-gc",
    );

    const baseline = await measureHeap(page);
    expect(baseline, "heap baseline indisponivel").not.toBeNull();

    // Stress: 10 navegacoes alternadas
    for (let i = 0; i < 10; i++) {
      await page.goto("/admin/alunos");
      await page.waitForLoadState("domcontentloaded");
      await page.goto("/admin/treinadores");
      await page.waitForLoadState("domcontentloaded");
    }

    await forceGC(page);
    const final = await measureHeap(page);
    expect(final).not.toBeNull();

    const ratio = final!.usedJSHeapSize / baseline!.usedJSHeapSize;
    expect(
      ratio,
      `heap cresceu ${ratio.toFixed(2)}x (baseline ${baseline!.usedJSHeapSize} -> ${final!.usedJSHeapSize})`,
    ).toBeLessThan(1.5);
  });
});
