import { describe, expect, it } from "vitest";
import { PUBLIC_PATHS } from "@/proxy";
import { PUBLIC_ROUTES as SITEMAP_ROUTES } from "@/app/sitemap";
import { PUBLIC_PAGES } from "../../e2e/specs/a11y/public-pages";
import lighthouserc from "../../lighthouserc.json";

/**
 * Guard contra o incidente já ocorrido 2x neste repo (AD-007/AD-013 em
 * .specs/STATE.md): rota nova entra no sitemap/lighthouse/e2e mas fica de
 * fora de PUBLIC_PATHS do proxy — visitante anônimo é bounced pra /login
 * antes de ver uma página que o resto do sistema já trata como pública.
 */

function stripQuery(path: string): string {
  return path.split("?")[0];
}

function isPublicPerProxy(path: string): boolean {
  return PUBLIC_PATHS.includes(path) || path.startsWith("/cadastro/");
}

const lighthousePublicPaths = lighthouserc.ci.collect.url.map(
  (url) => new URL(url).pathname,
);

describe("consistência entre os 4 registros de rota pública", () => {
  it.each(SITEMAP_ROUTES)("sitemap.ts: %s é pública no proxy.ts", (route) => {
    expect(isPublicPerProxy(route)).toBe(true);
  });

  it.each(lighthousePublicPaths)(
    "lighthouserc.json: %s é pública no proxy.ts",
    (path) => {
      expect(isPublicPerProxy(path)).toBe(true);
    },
  );

  it("lighthouserc.json cobre exatamente o mesmo conjunto indexável do sitemap.ts", () => {
    expect(new Set(lighthousePublicPaths)).toEqual(new Set(SITEMAP_ROUTES));
  });

  it.each(SITEMAP_ROUTES)(
    "sitemap.ts: %s tem scan de a11y E2E em all-pages-axe.spec.ts",
    (route) => {
      expect(PUBLIC_PAGES.map(stripQuery)).toContain(route);
    },
  );

  it.each(PUBLIC_PAGES)(
    "all-pages-axe.spec.ts: %s é pública no proxy.ts",
    (page) => {
      expect(isPublicPerProxy(stripQuery(page))).toBe(true);
    },
  );
});
