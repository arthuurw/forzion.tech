import { http, HttpResponse } from "msw";
import type { HttpHandler } from "msw";

// Defaults MSW para endpoints `/aluno/...` do backend.
// Política: 401 por padrão; testes sobrepõem com dados concretos via server.use().
const unauthorized = () => HttpResponse.json({ title: "Não autenticado", status: 401 }, { status: 401 });

export const alunoHandlers: HttpHandler[] = [
  http.get("*/aluno/dashboard", unauthorized),
  http.get("*/aluno/vinculo", unauthorized),
  http.get("*/aluno/me", unauthorized),
  http.get("*/aluno/treinador", unauthorized),
  http.get("*/aluno/fichas", unauthorized),
  http.get("*/aluno/fichas/:id", unauthorized),
  http.post("*/aluno/fichas/:id/executar", unauthorized),
  http.get("*/aluno/execucoes", unauthorized),
  http.post("*/aluno/execucoes", unauthorized),
  http.get("*/aluno/progressao", unauthorized),
  http.get("*/aluno/assinatura", unauthorized),
  http.post("*/aluno/assinatura/cancelar", unauthorized),
  http.post("*/aluno/vinculo/solicitar", unauthorized),
  http.post("*/aluno/vinculo/trocar-treinador", unauthorized),
  http.get("*/treinadores", unauthorized),
  http.get("*/treinadores/:id/pacotes", unauthorized),
];
