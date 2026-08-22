import { NextRequest, NextResponse } from "next/server";
import { forwardedForHeader } from "@/lib/security/forwardedFor";

const API_BASE = process.env.API_BASE_URL ?? "https://localhost:7220";

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ token: string }> }
) {
  const { token } = await params;
  const res = await fetch(`${API_BASE}/auth/convite/${encodeURIComponent(token)}`, {
    cache: "no-store",
    headers: { ...forwardedForHeader(request) },
  });
  const data = await res.json();
  return NextResponse.json(data, { status: res.status });
}
