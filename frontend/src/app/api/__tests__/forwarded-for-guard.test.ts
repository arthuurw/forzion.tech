import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync } from "node:fs";
import { join, resolve } from "node:path";

const API_DIR = resolve(import.meta.dirname, "..");
const SESSION_COOKIES = resolve(import.meta.dirname, "../../../lib/auth/sessionCookies.ts");

// Rotas cujo fetch usa a Data Cache compartilhada (`next: { revalidate }`). A chave
// dessa cache inclui TODOS os headers da request, então emitir o IP por cliente
// fragmentaria a cache por visitante e o backend passaria a ser chamado a cada
// request em vez de uma vez por janela. Além disso, um preenchimento de cache é
// feito em nome de todos os usuários, não de um IP — particioná-lo por IP seria
// semanticamente errado. São leituras públicas anônimas, na política `read`.
const CACHED_SHARED_READS = new Set([
  "auth/planos/route.ts",
  "auth/treinadores/route.ts",
  "auth/treinadores/[treinadorId]/pacotes/route.ts",
]);

// Exige a CHAMADA, não a menção: um import deixado para trás depois de o uso sumir
// não é apontado pelo eslint nem pelo tsc, e passaria por uma checagem de substring.
function emitsForwardedFor(source: string): boolean {
  return source.includes("forwardedForHeader(request)") || source.includes("...forwardedFor");
}

type ScannedFile = { relative: string; source: string };

function scanBackendCallers(): ScannedFile[] {
  const routes = readdirSync(API_DIR, { recursive: true, withFileTypes: true })
    .filter((entry) => entry.isFile() && entry.name === "route.ts")
    .map((entry) => {
      const absolute = join(entry.parentPath, entry.name);
      return {
        relative: absolute.slice(API_DIR.length + 1).replaceAll("\\", "/"),
        source: readFileSync(absolute, "utf8"),
      };
    });

  const files = [
    ...routes,
    { relative: "lib/auth/sessionCookies.ts", source: readFileSync(SESSION_COOKIES, "utf8") },
  ];

  return files.filter((file) => file.source.includes("${API_BASE}"));
}

describe("guard de repasse do IP real ao backend", () => {
  const callers = scanBackendCallers();

  it("a varredura alcança as duas árvores que chamam o backend", () => {
    const scanned = callers.map((f) => f.relative);

    expect(scanned).toContain("auth/route.ts");
    expect(scanned).toContain("backend/[...path]/route.ts");
    expect(scanned).toContain("lib/auth/sessionCookies.ts");
  });

  it("todo arquivo que monta URL do backend repassa o X-Forwarded-For", () => {
    const offenders = callers
      .filter((file) => !CACHED_SHARED_READS.has(file.relative))
      .filter((file) => !emitsForwardedFor(file.source))
      .map((file) => file.relative);

    expect(offenders).toEqual([]);
  });
});
