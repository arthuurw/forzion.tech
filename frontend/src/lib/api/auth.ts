// Uses plain fetch (NOT apiClient/axios): the /api/auth/* Next Route Handlers set
// the JWT as an httpOnly cookie. apiClient targets /api/backend with a Bearer header
// and never sees those cookies, so it cannot drive cookie-setting auth flows.
import type {
  IniciarPagamentoPlanoResponse,
  LoginResponse,
  MetodoPagamento,
  PacoteResponse,
  PlanoPlataformaResponse,
  ProblemDetails,
  TreinadorResponse,
} from "@/types";

// Thrown when an auth route responds non-2xx. Carries status + parsed ProblemDetails
// (when the body is JSON) so callers can branch on status/code without re-reading res.
export class AuthApiError extends Error {
  constructor(
    readonly status: number,
    readonly problem: ProblemDetails | null,
  ) {
    super(problem?.detail ?? problem?.title ?? `HTTP ${status}`);
    this.name = "AuthApiError";
  }
}

const JSON_HEADERS = { "Content-Type": "application/json" };

async function parseProblem(res: Response): Promise<ProblemDetails | null> {
  try {
    return (await res.json()) as ProblemDetails;
  } catch {
    return null;
  }
}

async function postJson<T>(url: string, body: unknown): Promise<T> {
  const res = await fetch(url, {
    method: "POST",
    headers: JSON_HEADERS,
    body: JSON.stringify(body),
  });
  if (!res.ok) throw new AuthApiError(res.status, await parseProblem(res));
  return (await res.json()) as T;
}

async function getJson<T>(url: string): Promise<T> {
  const res = await fetch(url);
  if (!res.ok) throw new AuthApiError(res.status, await parseProblem(res));
  return (await res.json()) as T;
}

export interface LoginPayload {
  email: string;
  senha: string;
}

export interface RegisterAlunoPayload {
  nome: string;
  email: string;
  telefone: string | null;
  senha: string;
  treinadorId: string;
  pacoteId: string;
  diasDisponiveis: number | null;
  tempoDisponivelMinutos: number | null;
  finalidade: string | null;
  focoTreino: string | null;
  nivelCondicionamento: string | null;
  limitacoesFisicas: string | null;
  doencas: string | null;
  observacoesAdicionais: string | null;
  consentimentoDadosSaude: boolean;
  consentimentoDadosSaudeEm: string;
}

export interface RegisterTreinadorPayload {
  nome: string;
  email: string;
  senha: string;
  telefone?: string;
  planoPlataformaId: string;
  modoPagamentoAluno: string;
}

export const authApi = {
  login(payload: LoginPayload) {
    return postJson<LoginResponse>("/api/auth", payload);
  },

  resendVerification(email: string) {
    return postJson<unknown>("/api/auth/resend-verification", { email });
  },

  listarTreinadores() {
    return getJson<TreinadorResponse[]>("/api/auth/treinadores");
  },

  listarPacotes(treinadorId: string) {
    return getJson<PacoteResponse[]>(`/api/auth/treinadores/${treinadorId}/pacotes`);
  },

  listarPlanos() {
    return getJson<PlanoPlataformaResponse[]>("/api/auth/planos");
  },

  registerAluno(payload: RegisterAlunoPayload) {
    return postJson<unknown>("/api/auth/register/aluno", payload);
  },

  registerTreinador(payload: RegisterTreinadorPayload) {
    return postJson<TreinadorResponse>("/api/auth/register/treinador", payload);
  },

  iniciarPagamentoTreinador(treinadorId: string, metodo: MetodoPagamento) {
    return postJson<IniciarPagamentoPlanoResponse>(
      `/api/auth/treinador/${treinadorId}/pagamento`,
      { metodo },
    );
  },
};
