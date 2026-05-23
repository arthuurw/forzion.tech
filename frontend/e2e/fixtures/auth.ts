import path from "node:path";
import fs from "node:fs";

export type AuthRole = "admin" | "aluno" | "treinador";

const AUTH_DIR = path.resolve(__dirname, "..", ".auth");

/**
 * Resolve o path absoluto do storage state pra uma role. Use em `test.use({...})`:
 *
 *   test.use({ storageState: authStatePath("admin") });
 *
 * Fase 10a: estrategia fail loud — se o arquivo nao existir (project "setup"
 * falhou ou nao rodou), Playwright erra com mensagem clara em runtime.
 */
export function authStatePath(role: AuthRole): string {
  return path.join(AUTH_DIR, `${role}.json`);
}

/**
 * Verifica existencia do storage state. Util para assertions explicitas em
 * tests que querem mensagem custom; por default, basta `test.use({ storageState })`
 * e deixar o Playwright explodir se ausente.
 */
export function hasAuthState(role: AuthRole): boolean {
  return fs.existsSync(authStatePath(role));
}
