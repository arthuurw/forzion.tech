import { NextRequest, NextResponse } from "next/server";
import type { SessionUser, TipoConta } from "@/types";

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

export async function GET(request: NextRequest) {
  const token = request.cookies.get("token")?.value;
  const sessionGuard = request.cookies.get("session_guard")?.value;

  if (!token || !sessionGuard) return NextResponse.json(null);

  const payload = decodeJwtPayload(token);
  if (!payload?.conta_id || !payload?.tipo_conta || !payload?.perfil_id) return NextResponse.json(null);
  if (payload.exp && payload.exp * 1000 < Date.now()) return NextResponse.json(null);

  const user: SessionUser = {
    token,
    contaId: payload.conta_id,
    tipoConta: payload.tipo_conta as TipoConta,
    perfilId: payload.perfil_id,
  };

  return NextResponse.json(user);
}
