import { test as setup, expect } from "@playwright/test";
import path from "node:path";
import fs from "node:fs";

/**
 * Project "setup" — autentica 3 roles (admin/aluno/treinador) via `/api/auth` e
 * persiste storage states em e2e/.auth/<role>.json. Specs subsequentes usam:
 *
 *   test.use({ storageState: ".auth/admin.json" });
 *
 * Credenciais via env vars (fail loud se ausentes):
 *   E2E_ADMIN_EMAIL / E2E_ADMIN_PASSWORD
 *   E2E_ALUNO_EMAIL / E2E_ALUNO_PASSWORD
 *   E2E_TREINADOR_EMAIL / E2E_TREINADOR_PASSWORD
 *
 * Fase 10a: estrategia "fail loud" — env var ausente faz o setup falhar com
 * mensagem explicita, forcando configuracao consciente antes de rodar specs.
 */

const AUTH_DIR = path.resolve(__dirname, ".auth");

type Role = "admin" | "aluno" | "treinador";

interface RoleEnv {
  role: Role;
  emailVar: string;
  passwordVar: string;
}

const ROLES: RoleEnv[] = [
  { role: "admin", emailVar: "E2E_ADMIN_EMAIL", passwordVar: "E2E_ADMIN_PASSWORD" },
  { role: "aluno", emailVar: "E2E_ALUNO_EMAIL", passwordVar: "E2E_ALUNO_PASSWORD" },
  { role: "treinador", emailVar: "E2E_TREINADOR_EMAIL", passwordVar: "E2E_TREINADOR_PASSWORD" },
];

setup.beforeAll(() => {
  if (!fs.existsSync(AUTH_DIR)) {
    fs.mkdirSync(AUTH_DIR, { recursive: true });
  }
});

for (const { role, emailVar, passwordVar } of ROLES) {
  setup(`autentica role ${role}`, async ({ request }) => {
    const email = process.env[emailVar];
    const password = process.env[passwordVar];

    expect(
      email,
      `${emailVar} ausente — configure secrets antes de rodar E2E`,
    ).toBeTruthy();
    expect(
      password,
      `${passwordVar} ausente — configure secrets antes de rodar E2E`,
    ).toBeTruthy();

    const response = await request.post("/api/auth", {
      data: { email, senha: password },
    });

    expect(
      response.ok(),
      `${role}: login falhou (${response.status()}) — verifique creds em ${emailVar}/${passwordVar}`,
    ).toBeTruthy();

    const statePath = path.join(AUTH_DIR, `${role}.json`);
    await request.storageState({ path: statePath });

    expect(fs.existsSync(statePath)).toBeTruthy();
  });
}
