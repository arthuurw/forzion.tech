import { http, HttpResponse } from "msw";
import type { HttpHandler } from "msw";

// Defaults MSW para endpoints `/pagamentos/...` do backend.
// Política: 401 por padrão; testes sobrepõem com dados concretos via server.use().
//
// Endpoints `/webhooks/stripe` e `/webhooks/resend` ficam DE PROPÓSITO fora dos
// defaults: são server-to-server (Stripe/Resend → backend, nunca chamados pelo
// frontend). Se algum teste de frontend tentar hitá-los, `onUnhandledRequest: "error"`
// pega — sem default silencioso de 401. Webhook flow é coberto pelos handler tests
// do backend (ProcessarWebhookStripeHandlerTests etc.).
const unauthorized = () => HttpResponse.json({ title: "Não autenticado", status: 401 }, { status: 401 });

export const pagamentoHandlers: HttpHandler[] = [
  http.get("*/pagamentos/:id/status", unauthorized),
  http.get("*/pagamentos/assinatura/:assinaturaId", unauthorized),
  http.get("*/treinador/modo-pagamento/preview", () =>
    HttpResponse.json({ assinaturasAtivasAlunos: 0, vinculosCobravelSemAssinatura: 0 })),
  http.get("*/treinador/pagamentos/recebimentos", () =>
    HttpResponse.json({ itens: [], proximoCursor: null })),
];
