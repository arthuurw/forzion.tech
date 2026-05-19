import { NextRequest, NextResponse } from "next/server";
import type { SessionUser, TipoConta } from "@/types";
import { parseJwtPayload } from "@/lib/auth/jwt";

export async function GET(request: NextRequest) {
  const token = request.cookies.get("token")?.value;
  const sessionGuard = request.cookies.get("session_guard")?.value;

  if (!token || !sessionGuard) return NextResponse.json(null);

  const payload = parseJwtPayload(token);
  if (!payload?.conta_id || !payload?.tipo_conta || !payload?.perfil_id) return NextResponse.json(null);
  if (payload.exp && payload.exp * 1000 < Date.now()) return NextResponse.json(null);

  // O token JWT NÃO é incluído na resposta — permanece apenas no httpOnly cookie.
  // Expor o token no JSON permitiria que JavaScript client-side o lesse.
  const user: SessionUser = {
    contaId: payload.conta_id,
    tipoConta: payload.tipo_conta as TipoConta,
    perfilId: payload.perfil_id,
  };

  return NextResponse.json(user);
}
