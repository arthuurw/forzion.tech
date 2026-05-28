import { http, HttpResponse } from "msw";
import type { HttpHandler } from "msw";

// Defaults MSW para endpoints `/pagamentos/...` do backend.
// Política: 401 por padrão; testes sobrepõem com dados concretos via server.use().
const unauthorized = () => HttpResponse.json({ title: "Não autenticado", status: 401 }, { status: 401 });

export const pagamentoHandlers: HttpHandler[] = [
  http.get("*/pagamentos/:id/status", unauthorized),
  http.get("*/pagamentos/assinatura/:assinaturaId", unauthorized),
  http.post("*/webhooks/stripe", unauthorized),
  http.post("*/webhooks/resend", unauthorized),
];
