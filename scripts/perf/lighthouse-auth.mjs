// Lighthouse autenticado (FR-4 / AC-4.1) nas rotas LOGADAS pesadas. Para cada role,
// loga nas contas bench no backend (:5080), injeta a sessão como cookies no Chrome do
// lighthouse (extraHeaders), roda preset=desktop 3×/rota e reporta a MEDIANA de
// LCP/TBT/CLS/FCP + transfer total e bytes de script por rota. REPORT-ONLY (budgets ALVO).
//
// Pré: backend perf_bench em :5080 (run-api), frontend prod em :3000 (API_BASE_URL=:5080,
// JWT_SECRET/ISSUER/AUDIENCE = bench), patch-loadtest.sql aplicado (contas + admin/system_user).
//
//   node scripts/perf/lighthouse-auth.mjs
import { execFileSync } from "node:child_process";
import { readFileSync, writeFileSync, mkdirSync, rmSync } from "node:fs";
import path from "node:path";

const BACKEND = process.env.BENCH_BACKEND || "http://localhost:5080";
const FE = process.env.BENCH_FRONTEND || "http://localhost:3000";
const SENHA = "Bench@123456";
const FRONTEND_DIR = path.resolve(import.meta.dirname, "../../frontend");
const LH = path.join(FRONTEND_DIR, "node_modules", ".bin", process.platform === "win32" ? "lighthouse.cmd" : "lighthouse");
const OUT = path.resolve(import.meta.dirname, "../../../perf-out/lh-auth");
const RUNS = 3;

// editor de ficha = treino de treinador1 (patch-loadtest). id estável via md5('treinador-1').
const TREINO_ID = process.env.BENCH_TREINO_ID || "5f04eccb-2d40-6965-ad0f-3a4e5431862b";

const PLAN = [
  { role: "aluno", tipo: "Aluno", email: "aluno1@bench.local",
    routes: [["dashboard-aluno", "/aluno"], ["historico-aluno", "/aluno/historico"]] },
  { role: "treinador", tipo: "Treinador", email: "treinador1@bench.local",
    routes: [["dashboard-treinador", "/treinador"], ["editor-ficha", `/treinador/treinos/${TREINO_ID}`]] },
  { role: "admin", tipo: "SystemAdmin", email: "admin1@bench.local",
    routes: [["dashboard-admin", "/admin"]] },
];

async function login(email) {
  const r = await fetch(`${BACKEND}/auth/login`, {
    method: "POST", headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, senha: SENHA }),
  });
  if (!r.ok) throw new Error(`login ${email} → ${r.status} ${await r.text()}`);
  return r.json();
}

function cookieHeader(tipo, data) {
  const consent = encodeURIComponent(JSON.stringify({ v: 1, analytics: false }));
  const guard = crypto.randomUUID();
  return [
    `token=${data.token}`, `refresh=${data.refreshToken}`,
    `tipo_conta=${tipo}`, `session_guard=${guard}`, `consent=${consent}`,
  ].join("; ");
}

function median(xs) {
  const s = [...xs].sort((a, b) => a - b);
  return s[Math.floor(s.length / 2)];
}

function lhRun(url, headersFile, outFile) {
  // chrome-launcher no Windows sai com código 1 num erro de cleanup de tmp DEPOIS de
  // escrever o relatório — tolerar e validar pelo arquivo de saída, não pelo exit code.
  try {
    execFileSync(LH, [
      url, "--preset=desktop", "--only-categories=performance",
      "--output=json", `--output-path=${outFile}`,
      `--extra-headers=${headersFile}`,
      '--chrome-flags=--headless=new --no-sandbox --disable-dev-shm-usage',
      "--quiet", "--max-wait-for-load=45000",
    ], { stdio: ["ignore", "ignore", "ignore"], shell: process.platform === "win32" });
  } catch { /* exit!=0 tolerado; o relatório abaixo é a fonte da verdade */ }
  const j = JSON.parse(readFileSync(outFile, "utf8"));
  const a = j.audits;
  const scriptBytes = (a["resource-summary"]?.details?.items || [])
    .find((i) => i.resourceType === "script")?.transferSize ?? 0;
  return {
    lcp: a["largest-contentful-paint"].numericValue,
    tbt: a["total-blocking-time"].numericValue,
    cls: a["cumulative-layout-shift"].numericValue,
    fcp: a["first-contentful-paint"].numericValue,
    score: j.categories.performance.score,
    totalKB: (a["total-byte-weight"]?.numericValue ?? 0) / 1024,
    scriptKB: scriptBytes / 1024,
  };
}

rmSync(OUT, { recursive: true, force: true });
mkdirSync(OUT, { recursive: true });
const results = [];

for (const { role, tipo, email, routes } of PLAN) {
  const data = await login(email);
  const headersFile = path.join(OUT, `headers-${role}.json`);
  writeFileSync(headersFile, JSON.stringify({ Cookie: cookieHeader(tipo, data) }));
  for (const [name, route] of routes) {
    const samples = [];
    for (let i = 1; i <= RUNS; i++) {
      console.error(`» ${role}:${name} run ${i}/${RUNS}`);
      samples.push(lhRun(`${FE}${route}`, headersFile, path.join(OUT, `${name}-${i}.json`)));
    }
    const med = {};
    for (const k of ["lcp", "tbt", "cls", "fcp", "score", "totalKB", "scriptKB"]) med[k] = median(samples.map((s) => s[k]));
    results.push({ role, route: name, path: route, ...med });
  }
}

writeFileSync(path.join(OUT, "results.json"), JSON.stringify(results, null, 2));
const f = (n, d = 0) => Number(n).toFixed(d);
console.log("\n| rota | path | LCP(ms) | TBT(ms) | CLS | FCP(ms) | perf | total(KB) | script(KB) |");
console.log("|---|---|---|---|---|---|---|---|---|");
for (const r of results)
  console.log(`| ${r.route} | \`${r.path}\` | ${f(r.lcp)} | ${f(r.tbt)} | ${f(r.cls, 3)} | ${f(r.fcp)} | ${f(r.score * 100)} | ${f(r.totalKB)} | ${f(r.scriptKB)} |`);
