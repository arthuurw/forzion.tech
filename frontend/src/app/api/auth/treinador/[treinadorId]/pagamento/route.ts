import { NextRequest, NextResponse } from "next/server";
import { checkRateLimit, getClientIp } from "@/lib/rateLimit";

const API_BASE = process.env.API_BASE_URL ?? "https://localhost:7220";

export async function POST(
  request: NextRequest,
  { params }: { params: Promise<{ treinadorId: string }> },
) {
  const ip = getClientIp(request);
  if (!checkRateLimit(ip)) {
    return NextResponse.json({ error: "Too many requests" }, { status: 429 });
  }

  const { treinadorId } = await params;

  let body: unknown;
  try {
    body = await request.json();
  } catch {
    return NextResponse.json(
      { title: "Corpo da requisição inválido.", status: 400 },
      { status: 400 },
    );
  }

  const res = await fetch(`${API_BASE}/auth/treinador/${treinadorId}/pagamento`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });

  const data = await res.json();
  return NextResponse.json(data, { status: res.status });
}
