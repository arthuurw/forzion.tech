import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync } from "node:fs";
import { join, resolve } from "node:path";

const AUTH_DIR = resolve(import.meta.dirname, "..");

// logout usa isCrossOrigin diretamente (mesma proteção do wrapper, só que inline) —
// não precisa de withSameOrigin.
const EXEMPTIONS = new Set(["logout/route.ts"]);

function hasPostHandler(source: string): boolean {
  return /export\s+(async\s+function\s+POST|const\s+POST\s*=)/.test(source);
}

function isProtectedAgainstCrossOrigin(source: string): boolean {
  return source.includes("withSameOrigin(") || source.includes("isCrossOrigin(");
}

type ScannedFile = { relative: string; source: string };

function scanPostHandlers(): ScannedFile[] {
  return readdirSync(AUTH_DIR, { recursive: true, withFileTypes: true })
    .filter((entry) => entry.isFile() && entry.name === "route.ts")
    .map((entry) => {
      const absolute = join(entry.parentPath, entry.name);
      return {
        relative: absolute.slice(AUTH_DIR.length + 1).replaceAll("\\", "/"),
        source: readFileSync(absolute, "utf8"),
      };
    })
    .filter((file) => hasPostHandler(file.source));
}

describe("guard de proteção cross-origin das rotas de sessão", () => {
  const handlers = scanPostHandlers();

  it("a varredura alcança os handlers POST conhecidos de auth/**", () => {
    const relatives = handlers.map((f) => f.relative);

    expect(relatives).toContain("route.ts");
    expect(relatives).toContain("logout/route.ts");
    expect(relatives).toContain("refresh/route.ts");
    expect(relatives).toContain("treinador/[treinadorId]/pagamento/route.ts");
    expect(relatives.length).toBeGreaterThanOrEqual(12);
  });

  it("todo handler POST de auth/** está protegido contra requisição cross-origin", () => {
    const offenders = handlers
      .filter((file) => !EXEMPTIONS.has(file.relative))
      .filter((file) => !isProtectedAgainstCrossOrigin(file.source))
      .map((file) => file.relative);

    expect(offenders).toEqual([]);
  });
});
