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

  const cookieOpts = {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax" as const,
    path: "/",
    maxAge: 60 * 60 * 8, // 8 horas
  };

  const response = NextResponse.json(data);
  response.cookies.set("token", data.token, cookieOpts);
  response.cookies.set("tipoConta", data.tipoConta, cookieOpts);

  return response;
}
