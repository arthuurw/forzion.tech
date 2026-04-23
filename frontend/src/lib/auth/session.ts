import type { SessionUser, TipoConta } from "@/types";

function getCookie(name: string): string | null {
  if (typeof document === "undefined") return null;
  const match = document.cookie.match(new RegExp(`(^| )${name}=([^;]+)`));
  return match ? decodeURIComponent(match[2]) : null;
}

interface JwtPayload {
  conta_id?: string;
  tipo_conta?: string;
  perfil_id?: string;
  exp?: number;
}

function decodeJwtPayload(token: string): JwtPayload | null {
  try {
    const parts = token.split(".");
    if (parts.length !== 3) return null;
    const payload = parts[1].replace(/-/g, "+").replace(/_/g, "/");
    return JSON.parse(atob(payload)) as JwtPayload;
  } catch {
    return null;
  }
}

export function loadSession(): SessionUser | null {
  const token = getCookie("token_access");
  if (!token) return null;

  const payload = decodeJwtPayload(token);
  if (!payload?.conta_id || !payload?.tipo_conta || !payload?.perfil_id) return null;

  if (payload.exp && payload.exp * 1000 < Date.now()) return null;

  return {
    token,
    contaId: payload.conta_id,
    tipoConta: payload.tipo_conta as TipoConta,
    perfilId: payload.perfil_id,
  };
}
