import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import path from "node:path";

const B = process.env.PROVISION_BACKEND ?? "http://localhost:8080";
const DIR = path.resolve(import.meta.dirname, "../reports");
mkdirSync(DIR, { recursive: true });

async function call(method, pathname, { token, body } = {}) {
  const h = { "content-type": "application/json" };
  if (token) h.authorization = `Bearer ${token}`;
  const res = await fetch(`${B}${pathname}`, {
    method, headers: h,
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await res.text();
  let json; try { json = text ? JSON.parse(text) : undefined; } catch { json = text; }
  return { status: res.status, json, text };
}
const env = {};
for (const line of readFileSync(path.resolve(import.meta.dirname, "../../../scratchpad-e2e.env"), "utf8").trim().split("\n")) {
  const [k, ...v] = line.split("="); env[k] = v.join("=");
}
const login = async (e, s) => (await call("POST", "/auth/login", { body: { email: e, senha: s } })).json?.token;

const results = [];
const step = (label, status, ok, extra = "") => {
  results.push({ label, status, ok, extra });
  console.log(`${ok ? "ok  " : "FAIL"} ${label} -> ${status} ${extra}`);
};

const t = await login(env.E2E_TREINADOR_EMAIL, env.E2E_TREINADOR_PASSWORD);
const a = await login(env.E2E_ALUNO_EMAIL, env.E2E_ALUNO_PASSWORD);

const grupos = (await call("GET", "/treinador/grupos-musculares", { token: t })).json;
const grupoId = (Array.isArray(grupos) ? grupos : grupos.items ?? grupos.dados)[0]?.id
  ?? (Array.isArray(grupos) ? grupos : grupos.items ?? grupos.dados)[0]?.grupoMuscularId;

// --- exercicio CRUD ---
const exNome = `Ex Sweep ${env.E2E_PACOTE_ID.slice(0, 8)}`;
const exCreate = await call("POST", "/treinador/exercicios", {
  token: t, body: { nome: exNome, grupoMuscularId: grupoId, descricao: "sweep" },
});
step("POST /treinador/exercicios", exCreate.status, exCreate.status >= 200 && exCreate.status < 300, exCreate.status >= 400 ? exCreate.text.slice(0, 90) : "");
const exId = exCreate.json?.exercicioId ?? exCreate.json?.id;
if (exId) {
  const exList = (await call("GET", "/treinador/exercicios", { token: t })).json;
  const arr = Array.isArray(exList) ? exList : exList.items ?? exList.dados ?? [];
  const found = arr.some((e) => (e.exercicioId ?? e.id) === exId);
  step("readback exercicio na lista", 200, found, found ? "" : "criado nao aparece na lista");
  const exPatch = await call("PATCH", `/treinador/exercicios/${exId}`, { token: t, body: { descricao: "sweep-edit" } });
  step("PATCH /treinador/exercicios/{id}", exPatch.status, exPatch.status >= 200 && exPatch.status < 300);
  const exDel = await call("DELETE", `/treinador/exercicios/${exId}`, { token: t });
  step("DELETE /treinador/exercicios/{id}", exDel.status, exDel.status >= 200 && exDel.status < 300);
}

// --- pacote CRUD ---
const pcCreate = await call("POST", "/treinador/pacotes", { token: t, body: { nome: "Pacote Sweep", preco: 50, descricao: "sweep" } });
step("POST /treinador/pacotes", pcCreate.status, pcCreate.status >= 200 && pcCreate.status < 300, pcCreate.status >= 400 ? pcCreate.text.slice(0, 90) : "");
const pcId = pcCreate.json?.pacoteId ?? pcCreate.json?.id;
if (pcId) {
  const pcPatch = await call("PATCH", `/treinador/pacotes/${pcId}`, { token: t, body: { preco: 75 } });
  step("PATCH /treinador/pacotes/{id}", pcPatch.status, pcPatch.status >= 200 && pcPatch.status < 300);
  const pcDel = await call("DELETE", `/treinador/pacotes/${pcId}`, { token: t });
  step("DELETE /treinador/pacotes/{id}", pcDel.status, pcDel.status >= 200 && pcDel.status < 300, pcDel.status >= 400 ? pcDel.text.slice(0, 90) : "");
}

// --- treino CRUD + exercicio nested ---
const trCreate = await call("POST", "/treinos", { token: t, body: { nome: "Treino Sweep", objetivo: 0, dificuldade: 0 } });
step("POST /treinos", trCreate.status, trCreate.status >= 200 && trCreate.status < 300, trCreate.status >= 400 ? trCreate.text.slice(0, 120) : "");
const trId = trCreate.json?.treinoId ?? trCreate.json?.id;
if (trId) {
  const trGet = await call("GET", `/treinos/${trId}`, { token: t });
  step("GET /treinos/{id} readback", trGet.status, trGet.status === 200);
  const trPatch = await call("PATCH", `/treinos/${trId}`, { token: t, body: { nome: "Treino Sweep Edit" } });
  step("PATCH /treinos/{id}", trPatch.status, trPatch.status >= 200 && trPatch.status < 300);
  const trDup = await call("POST", `/treinos/${trId}/duplicar`, { token: t });
  step("POST /treinos/{id}/duplicar", trDup.status, trDup.status >= 200 && trDup.status < 300, trDup.status >= 400 ? trDup.text.slice(0, 90) : "");
  const dupId = trDup.json?.treinoId ?? trDup.json?.id;
  if (dupId) await call("DELETE", `/treinos/${dupId}`, { token: t });
  const trDel = await call("DELETE", `/treinos/${trId}`, { token: t });
  step("DELETE /treinos/{id}", trDel.status, trDel.status >= 200 && trDel.status < 300);
}

// --- conta perfil patch (any) ---
const pf = await call("PATCH", "/conta/perfil", { token: a, body: { nome: "Aluno E2E" } });
step("PATCH /conta/perfil (aluno)", pf.status, pf.status >= 200 && pf.status < 300, pf.status >= 400 ? pf.text.slice(0, 90) : "");

// --- role isolation ---
const iso1 = await call("GET", "/admin/treinadores", { token: t });
step("ISO treinador->/admin/treinadores nega", iso1.status, iso1.status === 403);
const iso2 = await call("GET", "/treinador/alunos", { token: a });
step("ISO aluno->/treinador/alunos nega", iso2.status, iso2.status === 403);
const iso3 = await call("POST", "/treinador/pacotes", { token: a, body: { nome: "x", preco: 1 } });
step("ISO aluno->POST /treinador/pacotes nega", iso3.status, iso3.status === 403);

console.log(`\nTOTAL: ${results.filter((r) => r.ok).length}/${results.length} ok`);
writeFileSync(path.join(DIR, "mutations-sweep.json"), JSON.stringify(results, null, 2));
