import path from "node:path";
import fs from "node:fs";

export type AuthRole = "admin" | "aluno" | "treinador";

const AUTH_DIR = path.resolve(__dirname, "..", ".auth");

/**
 * Resolve o path absoluto do storage state pra uma role. Use em `test.use({...})`:
 *
 *   test.use({ storageState: authStatePath("admin") });
 */
export function authStatePath(role: AuthRole): string {
  return path.join(AUTH_DIR, `${role}.json`);
}

/**
 * Verifica se o storage state foi gerado pelo project "setup". Util pra
 * test.skip() em specs que precisam de auth real quando creds nao estao
 * configuradas (CI sem secrets, dev sem env).
 */
export function hasAuthState(role: AuthRole): boolean {
  return fs.existsSync(authStatePath(role));
}
