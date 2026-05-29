import axios from "axios";

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
      if (error.response?.status === 401) {
        window.location.href = "/login";
      } else if (
        error.response?.status === 403 &&
        error.response?.data?.code === "ASSINATURA_INADIMPLENTE"
      ) {
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
