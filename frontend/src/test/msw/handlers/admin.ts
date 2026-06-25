import { http, HttpResponse } from "msw";
import type { HttpHandler } from "msw";

// Defaults MSW para endpoints `/admin/...` do backend.
//
// Estratégia: retornar 401 por padrão (não autenticado). Testes que precisam
// de uma resposta concreta sobrepõem via `server.use(http.get("...admin..."))`.
//
// Sem esses defaults o policy `onUnhandledRequest: "error"` quebrava qualquer
// teste que não declarasse cada endpoint explicitamente; depois da migração
// Fase 5, o baseline torna network-failure testáveis (override pra 5xx, 408)
// sem boilerplate.
const unauthorized = () => HttpResponse.json({ title: "Não autenticado", status: 401 }, { status: 401 });

export const adminHandlers: HttpHandler[] = [
  http.get("*/admin/dashboard", unauthorized),
  http.get("*/admin/alunos", unauthorized),
  http.get("*/admin/alunos/:id", unauthorized),
  http.get("*/admin/alunos/:id/vinculo", unauthorized),
  http.get("*/admin/alunos/:id/fichas", unauthorized),
  http.get("*/admin/alunos/:id/execucoes", unauthorized),
  http.get("*/admin/fichas/:id", unauthorized),
  http.get("*/admin/treinadores", unauthorized),
  http.get("*/admin/treinadores/:id", unauthorized),
  http.post("*/admin/treinadores/:id/aprovar", unauthorized),
  http.post("*/admin/treinadores/:id/reprovar", unauthorized),
  http.post("*/admin/treinadores/:id/inativar", unauthorized),
  http.delete("*/admin/treinadores/:id", unauthorized),
  http.post("*/admin/treinadores/:id/plano", unauthorized),
  http.get("*/admin/planos", unauthorized),
  http.post("*/admin/planos", unauthorized),
  http.put("*/admin/planos/:id", unauthorized),
  http.delete("*/admin/planos/:id", unauthorized),
  http.get("*/admin/grupos-musculares", unauthorized),
  http.post("*/admin/grupos-musculares", unauthorized),
  http.put("*/admin/grupos-musculares/:id", unauthorized),
  http.delete("*/admin/grupos-musculares/:id", unauthorized),
  http.get("*/admin/saude/config", unauthorized),
  http.put("*/admin/saude/config", unauthorized),
  http.get("*/admin/saude/snapshots", unauthorized),
  http.post("*/admin/saude/executar", unauthorized),
  http.get("*/admin/stats/dashboard", unauthorized),
];
