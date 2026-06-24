import { writeFileSync, appendFileSync } from "node:fs";
import path from "node:path";

const BACKEND = process.env.PROVISION_BACKEND ?? "http://localhost:8080";
const REPO = path.resolve(import.meta.dirname, "../../..");

const ADMIN = { email: process.env.SEED_ADMIN_EMAIL ?? "admin@forzion.tech", senha: process.env.SEED_ADMIN_PASSWORD ?? "SeedAdmin!2026Local" };
const TREINADOR = { email: "treinador-e2e@e2e.test", senha: "E2e#Treinador2026" };
const ALUNO = { email: "aluno-e2e@e2e.test", senha: "E2e#Aluno2026" };

async function api(method, pathname, { token, body } = {}) {
  const headers = { "content-type": "application/json" };
  if (token) headers.authorization = `Bearer ${token}`;
  const res = await fetch(`${BACKEND}${pathname}`, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  const text = await res.text();
  let json;
  try { json = text ? JSON.parse(text) : undefined; } catch { json = text; }
  if (!res.ok) throw new Error(`${method} ${pathname} -> ${res.status}\n${text}`);
  return json;
}

const login = (email, senha) => api("POST", "/auth/login", { body: { email, senha } }).then((r) => r.token);

function list(r) { return Array.isArray(r) ? r : r.items ?? r.dados ?? []; }

async function main() {
  const adminToken = await login(ADMIN.email, ADMIN.senha);
  const treinadorToken = await login(TREINADOR.email, TREINADOR.senha);

  const alunos = list(await api("GET", "/treinador/alunos?tamanhoPagina=5", { token: treinadorToken }));
  const aluno = alunos.find((a) => a.email === ALUNO.email || a.nome === "Aluno E2E") ?? alunos[0];
  if (!aluno) throw new Error("nenhum aluno vinculado ao treinador E2E");
  const alunoId = aluno.alunoId ?? aluno.id;

  const exercicios = list(await api("GET", "/admin/exercicios?tamanhoPagina=1", { token: adminToken }));
  const exercicioId = exercicios[0]?.exercicioId;
  if (!exercicioId) throw new Error("nenhum exercicio global seeded");

  const existing = list(await api("GET", "/treinador/treinos?tamanhoPagina=20", { token: treinadorToken }));
  let treino = existing.find((t) => t.nome === "Treino E2E");
  if (!treino) {
    treino = await api("POST", "/treinos", {
      token: treinadorToken,
      body: { nome: "Treino E2E", objetivo: "Hipertrofia", dificuldade: "Iniciante" },
    });
    await api("POST", `/treinos/${treino.treinoId}/exercicios`, {
      token: treinadorToken,
      body: { exercicioId, series: [{ quantidade: 3, repeticoesMin: 10, repeticoesMax: 12, carga: 20, descanso: 60 }] },
    });
  }
  const treinoId = treino.treinoId;

  try {
    await api("POST", `/treinador/alunos/${alunoId}/fichas/${treinoId}`, { token: treinadorToken });
  } catch (e) {
    if (!String(e.message).includes("409")) throw e;
  }

  const alunoToken = await login(ALUNO.email, ALUNO.senha);
  const fichas = list(await api("GET", "/aluno/fichas?tamanhoPagina=5", { token: alunoToken }));
  const ficha = fichas[0];
  if (!ficha) throw new Error("aluno sem fichas apos vinculo");
  const fichaId = ficha.treinoAlunoId ?? ficha.id;

  const extra = [
    `E2E_ALUNO_ID=${alunoId}`,
    `E2E_TREINO_ID=${treinoId}`,
    `E2E_FICHA_ID=${fichaId}`,
  ].join("\n");

  appendFileSync(path.join(REPO, "scratchpad-e2e.env"), "\n" + extra + "\n");
  console.log("✓ fichas provisionadas:\n" + extra);
}

main().catch((e) => { console.error("✗ falha:\n", e.message); process.exit(1); });
