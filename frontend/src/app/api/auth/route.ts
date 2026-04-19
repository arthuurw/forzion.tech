import { NextRequest, NextResponse } from "next/server";
import type { LoginResponse } from "@/types";

const API_BASE = process.env.API_BASE_URL ?? "https://localhost:7220";

export async function POST(request: NextRequest) {
  const body = await request.json();

  const res = await fetch(`${API_BASE}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });

  if (!res.ok) {
    const error = await res.json();
    return NextResponse.json(error, { status: res.status });
  }

  const data: LoginResponse = await res.json();

  const baseOpts = {
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax" as const,
    path: "/",
    maxAge: 60 * 60 * 8,
  };

  const response = NextResponse.json(data);

  // httpOnly — apenas para o middleware de rotas do Next.js
  response.cookies.set("token", data.token, { ...baseOpts, httpOnly: true });
  response.cookies.set("tipoConta", data.tipoConta, { ...baseOpts, httpOnly: true });

  // Legíveis por JS — para o Axios client-side (Bearer) e AuthContext
  response.cookies.set("token_access", data.token, { ...baseOpts, httpOnly: false });
  response.cookies.set("user_data", JSON.stringify({
    token: data.token,
    tipoConta: data.tipoConta,
    contaId: data.contaId,
    perfilId: data.perfilId,
  }), { ...baseOpts, httpOnly: false });

  return response;
}
