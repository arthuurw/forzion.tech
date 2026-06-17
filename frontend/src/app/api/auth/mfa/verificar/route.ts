import { NextRequest, NextResponse } from "next/server";
import type { CompletarMfaResponse } from "@/types";
import { checkRateLimit, getClientIp } from "@/lib/rateLimit";
import {
  applySessionCookies,
  applyTrustedDeviceCookie,
  clearMfaPendingCookie,
} from "@/lib/auth/sessionCookies";

const API_BASE = process.env.API_BASE_URL ?? "https://localhost:7220";

export async function POST(request: NextRequest) {
  const ip = getClientIp(request);
  if (!checkRateLimit(ip)) {
    return NextResponse.json({ error: "Too many requests" }, { status: 429 });
  }

  const pending = request.cookies.get("mfa_pending")?.value;
  if (!pending) {
    return NextResponse.json({ title: "Sessão de verificação expirada.", status: 401 }, { status: 401 });
  }

  let body: unknown;
  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ title: "Corpo da requisição inválido.", status: 400 }, { status: 400 });
  }

  const res = await fetch(`${API_BASE}/auth/mfa/verificar`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Authorization: `Bearer ${pending}` },
    body: JSON.stringify(body),
  });

  if (!res.ok) {
    const error = await res.json();
    return NextResponse.json(error, { status: res.status });
  }

  const data: CompletarMfaResponse = await res.json();
  const { login, trustedDeviceToken } = data;

  const response = NextResponse.json({
    tipoConta: login.tipoConta,
    contaId: login.contaId,
    perfilId: login.perfilId,
    nome: login.nome,
  });
  applySessionCookies(response, { token: login.token, refreshToken: login.refreshToken, tipoConta: login.tipoConta });
  clearMfaPendingCookie(response);
  if (trustedDeviceToken) applyTrustedDeviceCookie(response, trustedDeviceToken);

  return response;
}
