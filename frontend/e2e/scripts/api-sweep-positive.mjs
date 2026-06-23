import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import path from "node:path";

const B = process.env.PROVISION_BACKEND ?? "http://localhost:8080";
const DIR = path.resolve(import.meta.dirname, "../reports");
mkdirSync(DIR, { recursive: true });
const KEY = process.env.INTERNAL_API_KEY;

async function call(method, pathname, { token, headers = {}, body, key } = {}) {
  const h = { "content-type": "application/json", ...headers };
  if (token) h.authorization = `Bearer ${token}`;
  if (key) h["X-Internal-Key"] = key;
  const res = await fetch(`${B}${pathname}`, {
    method, headers: h,
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await res.text();
  let json; try { json = text ? JSON.parse(text) : undefined; } catch { json = text; }
  return { status: res.status, json, text };
}
async function login(email, senha) {
  const r = await call("POST", "/auth/login", { body: { email, senha } });
  return r.json?.token;
}

const env = {};
for (const line of readFileSync(path.resolve(import.meta.dirname, "../../../scratchpad-e2e.env"), "utf8").trim().split("\n")) {
  const [k, ...v] = line.split("="); env[k] = v.join("=");
}

const results = [];
const rec = (group, label, status, ok, extra = "") =>
  results.push({ group, label, status, ok, extra });

const adminTok = await login(env.E2E_ADMIN_EMAIL, env.E2E_ADMIN_PASSWORD);
const treinTok = await login(env.E2E_TREINADOR_EMAIL, env.E2E_TREINADOR_PASSWORD);
const alunoTok = await login(env.E2E_ALUNO_EMAIL, env.E2E_ALUNO_PASSWORD);

const tId = env.E2E_TREINADOR_ID;
const alunos = (await call("GET", "/admin/alunos", { token: adminTok })).json;
const alunoList = Array.isArray(alunos) ? alunos : alunos?.items ?? alunos?.dados ?? [];
const alunoId = (alunoList[0]?.id ?? alunoList[0]?.alunoId);

const GETS = [
  ["admin", "GET /admin/stats/dashboard", "/admin/stats/dashboard", adminTok],
  ["admin", "GET /admin/treinadores", "/admin/treinadores", adminTok],
  ["admin", "GET /admin/treinadores/{id}", `/admin/treinadores/${tId}`, adminTok],
  ["admin", "GET /admin/planos", "/admin/planos", adminTok],
  ["admin", "GET /admin/grupos-musculares", "/admin/grupos-musculares", adminTok],
  ["admin", "GET /admin/exercicios", "/admin/exercicios", adminTok],
  ["admin", "GET /admin/alunos", "/admin/alunos", adminTok],
  ["admin", "GET /admin/alunos/{id}", `/admin/alunos/${alunoId}`, adminTok],
  ["admin", "GET /admin/alunos/{id}/vinculo", `/admin/alunos/${alunoId}/vinculo`, adminTok],
  ["admin", "GET /admin/alunos/{id}/fichas", `/admin/alunos/${alunoId}/fichas`, adminTok],
  ["admin", "GET /admin/alunos/{id}/execucoes", `/admin/alunos/${alunoId}/execucoes`, adminTok],
  ["admin", "GET /admin/alunos/{id}/progressao", `/admin/alunos/${alunoId}/progressao`, adminTok],
  ["admin", "GET /admin/treinadores/{id}/alunos", `/admin/treinadores/${tId}/alunos`, adminTok],
  ["admin", "GET /admin/treinadores/{id}/vinculos", `/admin/treinadores/${tId}/vinculos`, adminTok],
  ["admin", "GET /admin/treinadores/{id}/treinos", `/admin/treinadores/${tId}/treinos`, adminTok],
  ["admin", "GET /admin/treinadores/{id}/pacotes", `/admin/treinadores/${tId}/pacotes`, adminTok],
  ["admin", "GET /admin/notas-fiscais", "/admin/notas-fiscais", adminTok],
  ["admin", "GET /admin/health-report/config", "/admin/health-report/config", adminTok],
  ["admin", "GET /admin/health-report/snapshots", "/admin/health-report/snapshots", adminTok],
  ["admin", "GET /admin/test-data/contas", "/admin/test-data/contas", adminTok],
  ["treinador", "GET /treinador/alunos", "/treinador/alunos", treinTok],
  ["treinador", "GET /treinador/vinculos", "/treinador/vinculos", treinTok],
  ["treinador", "GET /treinador/treinos", "/treinador/treinos", treinTok],
  ["treinador", "GET /treinador/grupos-musculares", "/treinador/grupos-musculares", treinTok],
  ["treinador", "GET /treinador/exercicios", "/treinador/exercicios", treinTok],
  ["treinador", "GET /exercicios", "/exercicios", treinTok],
  ["treinador", "GET /treinador/pacotes", "/treinador/pacotes", treinTok],
  ["treinador", "GET /treinador/dados-fiscais", "/treinador/dados-fiscais", treinTok],
  ["treinador", "GET /treinador/notas-fiscais", "/treinador/notas-fiscais", treinTok],
  ["treinador", "GET /treinador/onboarding/status", "/treinador/onboarding/status", treinTok],
  ["treinador", "GET /treinador/modo-pagamento/preview", "/treinador/modo-pagamento/preview", treinTok],
  ["treinador", "GET /treinador/pagamentos/recebimentos", "/treinador/pagamentos/recebimentos", treinTok],
  ["treinador", "GET /treinador/plano/assinatura", "/treinador/plano/assinatura", treinTok],
  ["aluno", "GET /alunos", "/alunos", alunoTok],
  ["aluno", "GET /aluno/assinatura", "/aluno/assinatura", alunoTok],
  ["aluno", "GET /aluno/vinculo", "/aluno/vinculo", alunoTok],
  ["aluno", "GET /aluno/fichas", "/aluno/fichas", alunoTok],
  ["aluno", "GET /aluno/execucoes", "/aluno/execucoes", alunoTok],
  ["aluno", "GET /aluno/progressao", "/aluno/progressao", alunoTok],
  ["any", "GET /conta/perfil (aluno)", "/conta/perfil", alunoTok],
  ["any", "GET /conta/mfa/status (aluno)", "/conta/mfa/status", alunoTok],
  ["any", "GET /conta/lgpd/exportar (aluno)", "/conta/lgpd/exportar", alunoTok],
  ["anon", "GET /auth/planos", "/auth/planos", null],
  ["anon", "GET /auth/treinadores", "/auth/treinadores", null],
  ["anon", "GET /health", "/health", null],
  ["anon", "GET /health/ready", "/health/ready", null],
];

for (const [group, label, pathname, tok] of GETS) {
  const r = await call("GET", pathname, { token: tok });
  rec(group, label, r.status, r.status >= 200 && r.status < 300,
    r.status >= 400 ? String(r.text).slice(0, 80) : "");
}

const wStripe = await call("POST", "/webhooks/stripe", { body: { id: "evt_x" } });
rec("webhook", "POST /webhooks/stripe sem assinatura", wStripe.status, wStripe.status === 400, "espera 400");
const wResend = await call("POST", "/webhooks/resend", { body: { type: "x" } });
rec("webhook", "POST /webhooks/resend sem assinatura", wResend.status, wResend.status === 400 || wResend.status === 401, "espera 400/401");
const wWa = await call("GET", "/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=errado&hub.challenge=123");
rec("webhook", "GET /webhooks/whatsapp token errado", wWa.status, wWa.status === 403, "espera 403");

const iNoKey = await call("GET", "/internal/lgpd/contas-elegiveis");
rec("internal", "GET /internal/lgpd/contas-elegiveis SEM key", iNoKey.status, iNoKey.status === 401, "espera 401");
const iKey = await call("GET", "/internal/lgpd/contas-elegiveis", { key: KEY });
rec("internal", "GET /internal/lgpd/contas-elegiveis COM key", iKey.status,
  iKey.status === 200 || iKey.status === 429, "espera 200 (ou 429 rate-limit)");

const byGroup = {};
for (const r of results) (byGroup[r.group] ??= []).push(r);
for (const g of Object.keys(byGroup)) {
  const rs = byGroup[g];
  console.log(`\n[${g}] ${rs.filter((r) => r.ok).length}/${rs.length} ok`);
  for (const r of rs.filter((r) => !r.ok)) console.log(`  FAIL ${r.label} -> ${r.status} ${r.extra}`);
}
console.log(`\nTOTAL: ${results.filter((r) => r.ok).length}/${results.length} ok`);
writeFileSync(path.join(DIR, "positive-sweep.json"), JSON.stringify(results, null, 2));
