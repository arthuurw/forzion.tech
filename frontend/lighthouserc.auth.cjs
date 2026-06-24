const fs = require("node:fs");
const path = require("node:path");

// Lighthouse a11y nas rotas AUTENTICADAS (1 por role). Espelha o gate a11y do axe E2E
// no Lighthouse. Sessao via cookies do storage-state do Playwright (e2e/.auth/<role>.json,
// ja com o cookie `consent` apos seed-consent). Sem storage-state => FALHA explicita
// (nunca score falso). Server assumido em pe (CI: build+start + auth.setup + seed-consent antes).

const BASE = process.env.LHCI_BASE_URL || "http://localhost:3000";
const role = process.env.LHCI_ROLE;
const ROUTES = { aluno: "/aluno", treinador: "/treinador", admin: "/admin" };

if (!role || !ROUTES[role]) {
  throw new Error("lighthouserc.auth: defina LHCI_ROLE=aluno|treinador|admin");
}

const statePath = path.resolve(__dirname, "e2e/.auth", `${role}.json`);
if (!fs.existsSync(statePath)) {
  throw new Error(
    `lighthouserc.auth: storage-state ausente (${statePath}) — rode auth.setup + seed-consent antes do lhci auth`,
  );
}
const cookies = JSON.parse(fs.readFileSync(statePath, "utf8")).cookies || [];
const cookieHeader = cookies.map((c) => `${c.name}=${c.value}`).join("; ");
if (!cookieHeader) {
  throw new Error(`lighthouserc.auth: sem cookies em ${statePath}`);
}

module.exports = {
  ci: {
    collect: {
      url: [`${BASE}${ROUTES[role]}`],
      numberOfRuns: 1,
      settings: {
        preset: "desktop",
        skipAudits: ["uses-http2"],
        chromeFlags: "--no-sandbox --disable-dev-shm-usage",
        extraHeaders: JSON.stringify({ Cookie: cookieHeader }),
      },
    },
    assert: {
      assertions: {
        "categories:accessibility": ["error", { minScore: 0.95 }],
      },
    },
    upload: { target: "temporary-public-storage" },
  },
};
