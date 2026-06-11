import axios from "axios";
import { extractApiErrorInfo } from "@/lib/api/extractApiError";

const baseURL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "/api/backend";

export const apiClient = axios.create({
  baseURL,
  headers: { "Content-Type": "application/json" },
});

/**
 * Nome do evento global disparado quando o backend retorna 403 com code
 * "ASSINATURA_INADIMPLENTE". UI listeners (ex.: toast no AppLayout) escutam
 * em `window` e exibem notificacao ao usuario.
 */
export const ASSINATURA_INADIMPLENTE_EVENT = "forzion:assinatura-inadimplente";

export const ASSINATURA_INADIMPLENTE_MESSAGE =
  "Esta acao esta bloqueada porque sua assinatura esta inadimplente. Va em Pagamentos para regularizar.";

// OBS-01: guarda o X-Request-Id da ultima resposta para o beforeSend do Sentry
// correlacionar erros de browser com logs estruturados do backend (instrumentation-client).
function gravarRequestId(headers: unknown) {
  if (typeof window === "undefined" || !headers) return;
  const id = (headers as Record<string, string | undefined>)["x-request-id"];
  if (id) (window as typeof window & { __lastRequestId?: string }).__lastRequestId = id;
}

apiClient.interceptors.response.use(
  (res) => {
    gravarRequestId(res.headers);
    return res;
  },
  (error) => {
    gravarRequestId(error?.response?.headers);
    if (typeof window !== "undefined") {
      const { status, code } = extractApiErrorInfo(error);
      if (status === 401) {
        window.location.href = "/login";
      } else if (status === 403 && code === "ASSINATURA_INADIMPLENTE") {
        // Dispatch evento global. AppLayout (ou outro listener) renderiza toast.
        // Nao redireciona: aluno pode ja estar no portal; so notifica.
        window.dispatchEvent(
          new CustomEvent(ASSINATURA_INADIMPLENTE_EVENT, {
            detail: { message: ASSINATURA_INADIMPLENTE_MESSAGE },
          }),
        );
      }
    }
    return Promise.reject(error);
  }
);
