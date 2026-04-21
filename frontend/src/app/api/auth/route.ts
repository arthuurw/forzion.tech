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
    // sem maxAge → session cookie, expira ao fechar o browser
  };

  const response = NextResponse.json(data);

  // httpOnly — para o middleware de rotas do Next.js (decodifica JWT para extrair tipoConta)
  response.cookies.set("token", data.token, { ...baseOpts, httpOnly: true });

  // Legível por JS — para o Axios client-side (Bearer) e AuthContext (decodifica claims do payload)
  response.cookies.set("token_access", data.token, { ...baseOpts, httpOnly: false });

  return response;
}
