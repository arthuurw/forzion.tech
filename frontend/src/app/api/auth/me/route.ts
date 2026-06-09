import { NextRequest, NextResponse } from "next/server";
import { jwtVerify } from "jose";
import type { SessionUser, TipoConta } from "@/types";

export async function GET(request: NextRequest) {
  const token = request.cookies.get("token")?.value;
  const sessionGuard = request.cookies.get("session_guard")?.value;

  if (!token || !sessionGuard) return NextResponse.json(null);

  const secret = new TextEncoder().encode(process.env.JWT_SECRET ?? "");

  try {
    const { payload } = await jwtVerify(token, secret, {
      issuer: process.env.JWT_ISSUER,
      audience: process.env.JWT_AUDIENCE,
    });

    const contaId = payload["conta_id"] as string | undefined;
    const tipoConta = payload["tipo_conta"] as string | undefined;
    const perfilId = payload["perfil_id"] as string | undefined;

    if (!contaId || !tipoConta || !perfilId) return NextResponse.json(null);

    const user: SessionUser = {
      contaId,
      tipoConta: tipoConta as TipoConta,
      perfilId,
      // tokens antigos (pré-claim) não têm nome: não invalida a sessão, só fica vazio
      nome: (payload["nome"] as string | undefined) ?? "",
    };

    return NextResponse.json(user);
  } catch {
    return NextResponse.json(null);
  }
}
