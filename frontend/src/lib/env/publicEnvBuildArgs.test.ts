import { readdirSync, readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, it, expect } from "vitest";

/**
 * `next build` congela toda NEXT_PUBLIC_* no bundle. Var lida no código mas ausente do
 * Dockerfile vira string vazia na imagem e NENHUMA env de runtime conserta — foi assim que
 * a imagem nasceu com Stripe.js desligado (getStripe() null) e robots.txt noindex.
 */

const FRONTEND_ROOT = resolve(__dirname, "../../..");
const ROOT_SOURCES = [
  "instrumentation-client.ts",
  "sentry.server.config.ts",
  "sentry.edge.config.ts",
];

function isTestFile(path: string): boolean {
  return /\.(test|spec|stories)\.[cm]?[jt]sx?$/.test(path);
}

function publicVarsUsadasNoCodigo(): Set<string> {
  const arquivos = readdirSync(resolve(FRONTEND_ROOT, "src"), { recursive: true, encoding: "utf8" })
    .filter((p) => /\.[cm]?[jt]sx?$/.test(p) && !isTestFile(p))
    .map((p) => resolve(FRONTEND_ROOT, "src", p))
    .concat(ROOT_SOURCES.map((p) => resolve(FRONTEND_ROOT, p)));

  const encontradas = new Set<string>();
  for (const arquivo of arquivos) {
    const conteudo = readFileSync(arquivo, "utf8");
    for (const match of conteudo.matchAll(/process\.env\.(NEXT_PUBLIC_[A-Z0-9_]+)/g)) {
      encontradas.add(match[1]);
    }
  }
  return encontradas;
}

function argsDeclaradosNoDockerfile(): Set<string> {
  const conteudo = readFileSync(resolve(FRONTEND_ROOT, "Dockerfile"), "utf8");
  return new Set(
    [...conteudo.matchAll(/^ARG (NEXT_PUBLIC_[A-Z0-9_]+)/gm)].map((match) => match[1]),
  );
}

function buildArgsDoReleaseImages(): Set<string> {
  const conteudo = readFileSync(
    resolve(FRONTEND_ROOT, "../.github/workflows/release-images.yml"),
    "utf8",
  );
  return new Set(
    [...conteudo.matchAll(/^\s+(NEXT_PUBLIC_[A-Z0-9_]+)=/gm)].map((match) => match[1]),
  );
}

describe("NEXT_PUBLIC_* — build-args do frontend", () => {
  it("toda var lida no código está declarada como ARG no Dockerfile", () => {
    const usadas = publicVarsUsadasNoCodigo();
    expect(usadas.size).toBeGreaterThan(0);

    const declaradas = argsDeclaradosNoDockerfile();
    const faltando = [...usadas].filter((nome) => !declaradas.has(nome)).sort();

    expect(faltando, "vars sem ARG no Dockerfile nascem vazias na imagem").toEqual([]);
  });

  it("todo build-arg do release-images.yml existe como ARG no Dockerfile", () => {
    const passadas = buildArgsDoReleaseImages();
    expect(passadas.size).toBeGreaterThan(0);

    const declaradas = argsDeclaradosNoDockerfile();
    const orfas = [...passadas].filter((nome) => !declaradas.has(nome)).sort();

    expect(orfas, "build-arg sem ARG correspondente é descartado silenciosamente").toEqual([]);
  });

  it("as vars que prod não pode herdar do default do código são passadas no release-images", () => {
    const passadas = buildArgsDoReleaseImages();

    expect([...passadas].sort()).toEqual(
      expect.arrayContaining([
        "NEXT_PUBLIC_INDEXABLE",
        "NEXT_PUBLIC_SITE_URL",
        "NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY",
      ]),
    );
  });
});
