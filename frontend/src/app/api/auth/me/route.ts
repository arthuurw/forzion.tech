import { NextRequest, NextResponse } from "next/server";
import { jwtVerify } from "jose";
import type { SessionUser, TipoConta } from "@/types";
import { applySessionCookies, clearSessionCookies, fetchBackendRefresh } from "@/lib/auth/sessionCookies";

function toSessionUser(payload: Record<string, unknown>): SessionUser | null {
  const contaId = payload["conta_id"] as string | undefined;
  const tipoConta = payload["tipo_conta"] as string | undefined;
  const perfilId = payload["perfil_id"] as string | undefined;
  if (!contaId || !tipoConta || !perfilId) return null;
  return {
    contaId,
    tipoConta: tipoConta as TipoConta,
    perfilId,
    // tokens antigos (pré-claim) não têm nome: não invalida a sessão, só fica vazio
    nome: (payload["nome"] as string | undefined) ?? "",
  };
}

export async function GET(request: NextRequest) {
  const token = request.cookies.get("token")?.value;
  const sessionGuard = request.cookies.get("session_guard")?.value;
  const refresh = request.cookies.get("refresh")?.value;

  const secret = new TextEncoder().encode(process.env.JWT_SECRET ?? "");

  if (token && sessionGuard) {
    try {
      const { payload } = await jwtVerify(token, secret, {
        issuer: process.env.JWT_ISSUER,
        audience: process.env.JWT_AUDIENCE,
      });
      const user = toSessionUser(payload);
      if (user) return NextResponse.json(user);
    } catch {
      // access expirado/inválido → tenta refresh abaixo (renovação silenciosa no server)
    }
  }

  // Access ausente/expirado mas refresh presente: rotaciona server-side antes de devolver
  // null, p/ a sessão sobreviver a um reload com access já vencido (15min) sem deslogar.
  if (refresh) {
    const data = await fetchBackendRefresh(refresh);
    if (data) {
      const user: SessionUser = {
        contaId: data.contaId,
        tipoConta: data.tipoConta,
        perfilId: data.perfilId,
        nome: data.nome,
      };
      const response = NextResponse.json(user);
      applySessionCookies(response, {
        token: data.token,
        refreshToken: data.refreshToken,
        tipoConta: data.tipoConta,
      });
      return response;
    }
    // refresh inválido/expirado/reuse → limpa cookies mortos
    const response = NextResponse.json(null);
    clearSessionCookies(response);
    return response;
  }

  return NextResponse.json(null);
}
