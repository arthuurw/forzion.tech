import type { TipoConta } from "@/types";

interface JwtPayload {
  conta_id?: string;
  tipo_conta?: string;
  perfil_id?: string;
  exp?: number;
}

const TIPO_CONTA_VALUES: readonly TipoConta[] = ["SystemAdmin", "Treinador", "Aluno"];

// Defense-in-depth: JWT is verified server-side, but a malformed claim value must not slip through as a valid TipoConta.
export function parseTipoConta(value: unknown): TipoConta | null {
  return TIPO_CONTA_VALUES.includes(value as TipoConta) ? (value as TipoConta) : null;
}

export function parseJwtPayload(token: string): JwtPayload | null {
  try {
    const parts = token.split(".");
    if (parts.length !== 3) return null;
    const payload = parts[1].replace(/-/g, "+").replace(/_/g, "/");
    return JSON.parse(atob(payload)) as JwtPayload;
  } catch {
    return null;
  }
}

export function extractTipoConta(token: string): TipoConta | null {
  const payload = parseJwtPayload(token);
  if (!payload) return null;
  if (payload.exp && payload.exp * 1000 < Date.now()) return null;
  return parseTipoConta(payload.tipo_conta);
}
