import { NextRequest, NextResponse } from "next/server";
import type { LoginResponse } from "@/types";
import { checkRateLimit, getClientIp } from "@/lib/rateLimit";

const API_BASE = process.env.API_BASE_URL ?? "https://localhost:7220";

function getTokenMaxAge(token: string): number | undefined {
  try {
    const payload = JSON.parse(atob(token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/")));
    if (payload.exp) return payload.exp - Math.floor(Date.now() / 1000);
  } catch {
    // JWT malformed — return undefined so cookie receives no maxAge (session cookie)
  }
  return undefined;
}

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

  const maxAge = getTokenMaxAge(data.token);
  const baseOpts = {
    secure: process.env.NODE_ENV === "production",
    sameSite: "strict" as const,
    path: "/",
    maxAge,
  };

  const { token, ...clientSafeData } = data;
  const response = NextResponse.json(clientSafeData);
  response.cookies.set("token", token, { ...baseOpts, httpOnly: true });
  response.cookies.set("session_guard", crypto.randomUUID(), { ...baseOpts, httpOnly: true });

  return response;
}
