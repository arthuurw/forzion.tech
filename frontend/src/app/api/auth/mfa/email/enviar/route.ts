import { NextRequest, NextResponse } from "next/server";
import { checkRateLimit, getClientIp } from "@/lib/rateLimit";

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

  const res = await fetch(`${API_BASE}/auth/mfa/email/enviar`, {
    method: "POST",
    headers: { Authorization: `Bearer ${pending}` },
  });

  if (!res.ok) {
    const error = await res.json().catch(() => ({ title: "Erro ao enviar código.", status: res.status }));
    return NextResponse.json(error, { status: res.status });
  }

  return NextResponse.json({ ok: true });
}
