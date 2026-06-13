import { NextRequest, NextResponse } from "next/server";
import { applySessionCookies, clearSessionCookies, fetchBackendRefresh } from "@/lib/auth/sessionCookies";

/**
 * Proxy de renovação silenciosa. Repassa o cookie httpOnly `refresh` ao backend
 * `/auth/refresh`, que rotaciona (single-use) + detecta reuso. Em sucesso reescreve
 * `token`/`refresh`/`tipo_conta` rotacionados; em qualquer falha limpa os cookies de
 * sessão e devolve 401 (caller redireciona p/ /login). Não roteado pelo BFF /api/backend
 * porque precisa do cookie refresh httpOnly, não do Bearer.
 */
export async function POST(request: NextRequest) {
  const refresh = request.cookies.get("refresh")?.value;
  if (!refresh) {
    const res = NextResponse.json(null, { status: 401 });
    clearSessionCookies(res);
    return res;
  }

  const data = await fetchBackendRefresh(refresh);
  if (!data) {
    const res = NextResponse.json(null, { status: 401 });
    clearSessionCookies(res);
    return res;
  }

  const { token, refreshToken, ...clientSafeData } = data;
  const response = NextResponse.json(clientSafeData);
  applySessionCookies(response, { token, refreshToken, tipoConta: data.tipoConta });
  return response;
}
