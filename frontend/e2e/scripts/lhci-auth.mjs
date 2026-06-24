import { execFileSync } from "node:child_process";
import { existsSync } from "node:fs";
import path from "node:path";

// Roda o gate Lighthouse a11y (0.95) em 1 rota por role autenticado, cada um com a sessao
// do storage-state. Server assumido em pe (LHCI_BASE_URL, default http://localhost:3000).
// FALHA explicita se faltar storage-state (provision-local + auth.setup + seed-consent).

const FRONTEND = path.resolve(import.meta.dirname, "../..");
const ROLES = ["aluno", "treinador", "admin"];

for (const role of ROLES) {
  const statePath = path.join(FRONTEND, "e2e", ".auth", `${role}.json`);
  if (!existsSync(statePath)) {
    console.error(`✗ storage-state ausente: ${statePath} — rode auth.setup + seed-consent`);
    process.exit(1);
  }
}

let failed = false;
for (const role of ROLES) {
  console.log(`\n» lhci a11y autenticado: ${role}`);
  try {
    execFileSync(
      "npx",
      ["lhci", "autorun", "--config=lighthouserc.auth.cjs"],
      { cwd: FRONTEND, stdio: "inherit", env: { ...process.env, LHCI_ROLE: role }, shell: true },
    );
  } catch {
    failed = true;
    console.error(`✗ lhci a11y falhou para role ${role}`);
  }
}

process.exit(failed ? 1 : 0);
