// Documento OpenAPI nativo so e servido em Development. Suba a API local com
// ASPNETCORE_ENVIRONMENT=Development antes de rodar.

import { writeFile } from "node:fs/promises";

const DEFAULT_URL = "http://localhost:5230/openapi/v1.json";
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
