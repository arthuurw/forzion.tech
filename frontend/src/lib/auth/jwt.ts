import type { TipoConta } from "@/types";

interface JwtPayload {
  conta_id?: string;
  tipo_conta?: string;
  perfil_id?: string;
  exp?: number;
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
  return (payload.tipo_conta as TipoConta) ?? null;
}
