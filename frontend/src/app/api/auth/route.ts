import { NextRequest, NextResponse } from "next/server";
import type { LoginResponse } from "@/types";
import { checkRateLimit, getClientIp } from "@/lib/rateLimit";
import { applySessionCookies, applyMfaPendingCookie } from "@/lib/auth/sessionCookies";

const API_BASE = process.env.API_BASE_URL ?? "https://localhost:7220";

export async function POST(request: NextRequest) {
  const ip = getClientIp(request);
  if (!checkRateLimit(ip)) {
    return NextResponse.json({ error: "Too many requests" }, { status: 429 });
  }

  let body: unknown;
  try {
    body = await request.json();
  } catch {
    return NextResponse.json(
      { title: "Corpo da requisição inválido.", status: 400 },
      { status: 400 },
    );
  }

  const trustedDevice = request.cookies.get("trusted_device")?.value;
  const res = await fetch(`${API_BASE}/auth/login`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      ...(trustedDevice ? { Cookie: `trusted_device=${trustedDevice}` } : {}),
    },
    body: JSON.stringify(body),
  });

  if (!res.ok) {
    const error = await res.json();
    return NextResponse.json(error, { status: res.status });
  }

  const data: LoginResponse = await res.json();

  if (data.mfaRequerido && data.mfaPendingToken) {
    const response = NextResponse.json({ mfaRequerido: true, mfaPendingExpiraEm: data.mfaPendingExpiraEm ?? null });
    applyMfaPendingCookie(response, data.mfaPendingToken);
    return response;
  }

  const response = NextResponse.json({
    tipoConta: data.tipoConta,
    contaId: data.contaId,
    perfilId: data.perfilId,
    nome: data.nome,
  });
  applySessionCookies(response, { token: data.token, refreshToken: data.refreshToken, tipoConta: data.tipoConta });

  return response;
}
