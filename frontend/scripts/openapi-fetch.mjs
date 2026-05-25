// Baixa o spec OpenAPI de uma instancia LOCAL do backend (Swagger so e servido
// em Development; nao e mais exposto em homolog/producao). Suba a API local
// (ASPNETCORE_ENVIRONMENT=Development) antes de rodar.
// Override via env: BACKEND_OPENAPI_URL=https://outra/swagger.json npm run openapi:fetch

import { writeFile } from "node:fs/promises";

const DEFAULT_URL = "http://localhost:5230/swagger/v1/swagger.json";
const url = process.env.BACKEND_OPENAPI_URL ?? DEFAULT_URL;

console.log(`Baixando OpenAPI spec: ${url}`);
const res = await fetch(url);
if (!res.ok) {
  console.error(`HTTP ${res.status} ${res.statusText}`);
  process.exit(1);
}

const text = await res.text();
await writeFile("openapi.json", text);
console.log(`OK (${text.length} bytes -> openapi.json)`);
