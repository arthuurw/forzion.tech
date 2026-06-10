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

apiClient.interceptors.response.use(
  (res) => res,
  (error) => {
    if (typeof window !== "undefined") {
      // Lê status+code pela mesma fonte de verdade dos demais callers (helper
      // central), em vez de cavar response.data.code inline.
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
