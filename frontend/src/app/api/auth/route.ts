import { NextRequest, NextResponse } from "next/server";
import type { LoginResponse } from "@/types";
import { checkRateLimit, getClientIp } from "@/lib/rateLimit";
import { accessTokenMaxAge, applySessionCookies, baseCookieOpts } from "@/lib/auth/sessionCookies";

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

  // `refresh` é httpOnly e NÃO pode vazar no corpo da resposta (fica só no cookie).
  const { token, refreshToken, ...clientSafeData } = data;
  const response = NextResponse.json(clientSafeData);
  applySessionCookies(response, { token, refreshToken, tipoConta: data.tipoConta });
  response.cookies.set("session_guard", crypto.randomUUID(), {
    ...baseCookieOpts(accessTokenMaxAge(token)),
    httpOnly: true,
  });

  return response;
}
