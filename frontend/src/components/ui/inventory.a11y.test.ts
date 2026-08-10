import { existsSync, readdirSync } from "node:fs";
import { join } from "node:path";
import { describe, it, expect } from "vitest";

const UI_DIR = join(import.meta.dirname, ".");
const FORMS_DIR = join(import.meta.dirname, "..", "forms");

const EXEMPTIONS: Record<string, string> = {
  "DataList.tsx":
    "Composicional — só compõe LoadingSpinner, EmptyState e ResponsiveTable, todos já cobertos por story+a11y-test próprios.",
  "ConsentProvider.tsx":
    "Wrapper dynamic(ssr:false) sem markup próprio (lê cookie) — o markup real é do ConsentBanner, já coberto.",
  "ErrorBoundary.tsx":
    "Class boundary; o markup visível em erro (fallback) é fornecido pelo chamador, não por este componente.",
};

function discoverComponentFiles(dir: string): string[] {
  return readdirSync(dir).filter(
    (entry) => entry.endsWith(".tsx") && !entry.endsWith(".stories.tsx") && !entry.endsWith(".test.tsx"),
  );
}

const targets = [
  ...discoverComponentFiles(UI_DIR).map((file) => ({ dir: UI_DIR, dirLabel: "ui", file })),
  ...discoverComponentFiles(FORMS_DIR).map((file) => ({ dir: FORMS_DIR, dirLabel: "forms", file })),
];

describe("inventory de acessibilidade: ui/ e forms/", () => {
  it("descobriu ao menos 1 componente em cada diretório", () => {
    expect(targets.some((t) => t.dirLabel === "ui")).toBe(true);
    expect(targets.some((t) => t.dirLabel === "forms")).toBe(true);
  });

  it.each(targets)("$dirLabel/$file tem story dedicada ou isenção declarada", ({ dir, file }) => {
    if (file in EXEMPTIONS) {
      expect(EXEMPTIONS[file].length).toBeGreaterThan(0);
      return;
    }
    const storyPath = join(dir, file.replace(/\.tsx$/, ".stories.tsx"));
    expect(existsSync(storyPath), `story ausente: ${storyPath}`).toBe(true);
  });

  it.each(targets)("$dirLabel/$file tem a11y test dedicado ou isenção declarada", ({ dir, file }) => {
    if (file in EXEMPTIONS) {
      expect(EXEMPTIONS[file].length).toBeGreaterThan(0);
      return;
    }
    const testPath = join(dir, file.replace(/\.tsx$/, ".a11y.test.tsx"));
    expect(existsSync(testPath), `a11y test ausente: ${testPath}`).toBe(true);
  });
});
