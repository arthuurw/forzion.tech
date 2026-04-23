import { NextRequest, NextResponse } from "next/server";

const API_BASE = process.env.API_BASE_URL ?? "https://localhost:7220";

export async function GET(
  _request: NextRequest,
  { params }: { params: Promise<{ treinadorId: string }> }
) {
  const { treinadorId } = await params;
  const res = await fetch(`${API_BASE}/auth/treinadores/${treinadorId}/pacotes`, {
    next: { revalidate: 60 },
  });
  const data = await res.json();
  return NextResponse.json(data, { status: res.status });
}
