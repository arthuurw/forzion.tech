import type { NextResponse } from "next/server";
import type { RefreshResponse } from "@/types";

const API_BASE = process.env.API_BASE_URL ?? "https://localhost:7220";

// Janela idle do refresh por papel (espelha Auth:Sessao no backend; design §7 R-LIFETIME).
// Só define quanto tempo o COOKIE persiste no browser — o backend é a autoridade do idle
// real (RefreshToken.ExpiraEm) e do absolute (RefreshTokenFamily). Cookie sobrevivente
// a um token já expirado server-side apenas resulta em 401 → /login no próximo refresh.
const REFRESH_MAX_AGE: Record<string, number> = {
  SystemAdmin: 2 * 60 * 60, // 2h
  Treinador: 7 * 24 * 60 * 60, // 7d
  Aluno: 7 * 24 * 60 * 60, // 7d
};
const DEFAULT_REFRESH_MAX_AGE = 7 * 24 * 60 * 60;

// Deriva o maxAge do cookie de access do `exp` do próprio JWT (igual ao login proxy).
export function accessTokenMaxAge(token: string): number | undefined {
  try {
    const payload = JSON.parse(atob(token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/")));
    if (payload.exp) return payload.exp - Math.floor(Date.now() / 1000);
  } catch {
    // JWT malformado → sem maxAge (cookie de sessão)
  }
  return undefined;
}

export function refreshMaxAge(tipoConta: string | undefined): number {
  return (tipoConta && REFRESH_MAX_AGE[tipoConta]) || DEFAULT_REFRESH_MAX_AGE;
}

export function baseCookieOpts(maxAge?: number) {
  return {
    secure: process.env.NODE_ENV === "production",
    sameSite: "strict" as const,
    path: "/",
    maxAge,
  };
}

/**
 * Seta os cookies de sessão (login + rotação de refresh):
 *   - `token`   httpOnly: access JWT curto.
 *   - `refresh` httpOnly: refresh raw (NUNCA exposto ao JS — só viaja browser↔proxy).
 *   - `tipo_conta` NÃO-httpOnly: hint de roteamento p/ o middleware (R-FE). Não é segredo;
 *     papel é decidido pelo backend via jti/policies. Permite rotear sem desencriptar o JWT.
 */
export function applySessionCookies(
  response: NextResponse,
  data: { token: string; refreshToken: string; tipoConta: string },
): void {
  const accessMaxAge = accessTokenMaxAge(data.token);
  const idleMaxAge = refreshMaxAge(data.tipoConta);
  response.cookies.set("token", data.token, { ...baseCookieOpts(accessMaxAge), httpOnly: true });
  response.cookies.set("refresh", data.refreshToken, { ...baseCookieOpts(idleMaxAge), httpOnly: true });
  response.cookies.set("tipo_conta", data.tipoConta, { ...baseCookieOpts(idleMaxAge), httpOnly: false });
  // session_guard re-emitido a CADA rotação, não só no login: gateia o jwtVerify em /me e no
  // middleware. Se não re-emitisse, expiraria junto do 1º access (~15min) e toda chamada cairia
  // no refresh path — rotação do refresh a cada /me + middleware preso no hint não-verificado.
  response.cookies.set("session_guard", crypto.randomUUID(), { ...baseCookieOpts(accessMaxAge), httpOnly: true });
}

export function clearSessionCookies(response: NextResponse): void {
  // path explícito: os cookies foram setados com Path=/; delete(name) sem path não casa o
  // Path=/ e deixaria o cookie morto sobreviver no browser (Set-Cookie só apaga path igual).
  for (const name of ["token", "refresh", "session_guard", "tipo_conta"])
    response.cookies.delete({ name, path: "/" });
}

/**
 * Chama o backend `/auth/refresh` repassando o refresh raw via cookie httpOnly
 * (o endpoint lê `Request.Cookies["refresh"]`, nunca body/JS). Retorna o par novo
 * rotacionado, ou null em qualquer falha (inválido/expirado/reuse/rede) → caller desloga.
 */
export async function fetchBackendRefresh(refreshRaw: string): Promise<RefreshResponse | null> {
  try {
    const res = await fetch(`${API_BASE}/auth/refresh`, {
      method: "POST",
      headers: { Cookie: `refresh=${refreshRaw}` },
    });
    if (!res.ok) return null;
    return (await res.json()) as RefreshResponse;
  } catch {
    return null;
  }
}
