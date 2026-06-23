import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import path from "node:path";

const B = process.env.PROVISION_BACKEND ?? "http://localhost:8080";
const DIR = path.resolve(import.meta.dirname, "../reports");
mkdirSync(DIR, { recursive: true });
const DUMMY = "00000000-0000-0000-0000-000000000000";

const rows = readFileSync(path.join(import.meta.dirname, "endpoints.tsv"), "utf8")
  .trim().split("\n").map((l) => {
    const [method, route, auth, stepUp] = l.split("\t");
    return { method, route, auth, stepUp: stepUp === "true" };
  });

function fill(route) {
  return route.replace(/\{[^}]+\}/g, DUMMY);
}

async function call(method, pathname, { headers = {}, body } = {}) {
  const res = await fetch(`${B}${pathname}`, {
    method,
    headers: { "content-type": "application/json", ...headers },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await res.text();
  return { status: res.status, text };
}

const results = [];
const PROTECTED = rows.filter((r) => r.auth !== "anonymous" && r.auth !== "webhook");

for (const r of PROTECTED) {
  const pathname = fill(r.route);
  const body = ["POST", "PUT", "PATCH"].includes(r.method) ? {} : undefined;
  const { status } = await call(r.method, pathname, { body });
  const pass = status === 401;
  results.push({ ...r, check: "unauth", status, expected: 401, pass });
}

const fails = results.filter((r) => !r.pass);
console.log(`AUTH-BOUNDARY: ${results.length - fails.length}/${results.length} retornaram 401 sem credencial`);
if (fails.length) {
  console.log("\nNAO-401 (investigar):");
  for (const f of fails) console.log(`  ${f.method} ${f.route} [${f.auth}] -> ${f.status}`);
}

writeFileSync(
  path.join(DIR, "auth-boundary.json"),
  JSON.stringify(results, null, 2),
);
