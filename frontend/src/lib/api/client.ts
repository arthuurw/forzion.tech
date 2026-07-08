import axios, { type AxiosError, type InternalAxiosRequestConfig } from "axios";
import { extractApiErrorInfo } from "@/lib/api/extractApiError";
import { requestStepUp } from "@/lib/auth/stepUpController";

const baseURL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "/api/backend";

export const apiClient = axios.create({
  baseURL,
  headers: { "Content-Type": "application/json" },
});

// Flag de retry por request: garante UMA tentativa de refresh (sem loop se o retry
// também 401 — ex.: família revogada / refresh já rotacionado).
type RetriableConfig = InternalAxiosRequestConfig & { _retry?: boolean; _stepUpRetry?: boolean };

// Promise de refresh em voo, compartilhada entre 401s concorrentes na MESMA aba
// (anti-tempestade): N requests que estouram juntos disparam 1 só chamada a /api/auth/refresh.
let refreshInFlight: Promise<boolean> | null = null;

const REFRESH_LOCK_NAME = "forzion:auth-refresh";

// Serializa entre ABAS via Web Locks: o cookie refresh httpOnly é compartilhado por
// origem, então duas abas renovando ao mesmo tempo reapresentam o mesmo token e uma
// delas dispara reuse-detection (revoga a família, desloga as duas). Fallback direto
// quando a API não existe (FEAUTH-05).
export function runExclusive<T>(fn: () => Promise<T>): Promise<T> {
  const locks = globalThis.navigator?.locks;
  if (!locks) return fn();
  return locks.request(REFRESH_LOCK_NAME, fn);
}

function renovarSessao(): Promise<boolean> {
  if (!refreshInFlight) {
    // fetch direto (não apiClient) p/ não recursar neste interceptor.
    refreshInFlight = runExclusive(() =>
      fetch("/api/auth/refresh", { method: "POST" })
        .then((r) => r.ok)
        .catch(() => false),
    ).finally(() => {
      refreshInFlight = null;
    });
  }
  return refreshInFlight;
}

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
  // Resposta sem o header (ex.: 204) LIMPA o id: sem isso, um erro posterior nao
  // relacionado a API herdaria a correlacao de uma request anterior alheia.
  (window as typeof window & { __lastRequestId?: string }).__lastRequestId = id || undefined;
}

apiClient.interceptors.response.use(
  (res) => {
    gravarRequestId(res.headers);
    return res;
  },
  async (error: AxiosError) => {
    gravarRequestId(error?.response?.headers);
    if (typeof window !== "undefined") {
      const { status, code } = extractApiErrorInfo(error);
      if (status === 401) {
        const original = error.config as RetriableConfig | undefined;
        // 1ª 401: tenta renovação silenciosa e refaz a request original com os
        // cookies rotacionados. Só desloga se o refresh falhar (sessão de fato morta).
        if (original && !original._retry) {
          original._retry = true;
          const renovou = await renovarSessao();
          if (renovou) return apiClient(original);
        }
        window.location.href = "/login";
      } else if (status === 403 && code === "step_up_requerido") {
        const original = error.config as RetriableConfig | undefined;
        if (original && !original._stepUpRetry) {
          original._stepUpRetry = true;
          const token = await requestStepUp();
          if (token) {
            original.headers.set("X-Step-Up-Token", token);
            return apiClient(original);
          }
        }
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
