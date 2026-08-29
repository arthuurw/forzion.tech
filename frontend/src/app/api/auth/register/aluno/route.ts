import { NextRequest, NextResponse } from "next/server";
import { checkRateLimit, getClientIp } from "@/lib/rateLimit";
import { forwardedForHeader } from "@/lib/security/forwardedFor";
import { withSameOrigin } from "@/lib/security/withSameOrigin";

const API_BASE = process.env.API_BASE_URL ?? "https://localhost:7220";

export const POST = withSameOrigin(async (request: NextRequest) => {
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

  const res = await fetch(`${API_BASE}/auth/register/aluno`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...forwardedForHeader(request) },
    body: JSON.stringify(body),
  });

  const data = await res.json();
  return NextResponse.json(data, { status: res.status });
});
