import { http, HttpResponse } from "msw";
import type { HttpHandler } from "msw";

// Defaults MSW para endpoints `/treinador/...` do backend.
// Política: 401 por padrão; testes sobrepõem com dados concretos via server.use().
const unauthorized = () => HttpResponse.json({ title: "Não autenticado", status: 401 }, { status: 401 });

export const treinadorHandlers: HttpHandler[] = [
  http.get("*/treinador/alunos", unauthorized),
  http.get("*/treinador/alunos/:id", unauthorized),
  http.post("*/treinador/alunos/:id/reativar", unauthorized),
  http.delete("*/treinador/alunos/:id", unauthorized),
  http.post("*/treinador/vinculos/:id/aprovar", unauthorized),
  http.post("*/treinador/vinculos/:id/desvincular", unauthorized),
  http.get("*/treinador/vinculos", unauthorized),
  http.get("*/treinador/pacotes", unauthorized),
  http.post("*/treinador/pacotes", unauthorized),
  http.put("*/treinador/pacotes/:id", unauthorized),
  http.delete("*/treinador/pacotes/:id", unauthorized),
  http.get("*/treinador/exercicios", unauthorized),
  http.post("*/treinador/exercicios", unauthorized),
  http.put("*/treinador/exercicios/:id", unauthorized),
  http.delete("*/treinador/exercicios/:id", unauthorized),
  http.post("*/treinador/exercicios/:id/copiar", unauthorized),
  http.get("*/treinador/treinos", unauthorized),
  http.post("*/treinador/treinos", unauthorized),
  http.get("*/treinador/treinos/:id", unauthorized),
  http.put("*/treinador/treinos/:id", unauthorized),
  http.delete("*/treinador/treinos/:id", unauthorized),
  http.post("*/treinador/treinos/:id/duplicar", unauthorized),
  http.post("*/treinador/treinos/:id/exercicios", unauthorized),
  http.put("*/treinador/treinos/:id/exercicios/:exId", unauthorized),
  http.delete("*/treinador/treinos/:id/exercicios/:exId", unauthorized),
  http.post("*/treinador/treinos/:id/vincular-aluno", unauthorized),
  http.get("*/treinador/onboarding", unauthorized),
  http.post("*/treinador/onboarding/iniciar", unauthorized),
  http.get("*/treinador/onboarding/verificar", unauthorized),
  http.get("*/treinador/pagamentos", unauthorized),
  http.post("*/treinador/pagamentos/cobrar/:assinaturaId", unauthorized),
];
