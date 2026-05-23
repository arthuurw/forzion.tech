// Baixa o spec OpenAPI do backend (homologacao por padrao).
// Override via env: BACKEND_OPENAPI_URL=https://outra/swagger.json npm run openapi:fetch

import { writeFile } from "node:fs/promises";

const DEFAULT_URL = "https://homologacao.forzion.tech/swagger/v1/swagger.json";
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
