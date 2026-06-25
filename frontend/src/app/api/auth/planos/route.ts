import { NextResponse } from "next/server";

const API_BASE = process.env.API_BASE_URL ?? "https://localhost:7220";

export async function GET() {
  const res = await fetch(`${API_BASE}/auth/planos`, {
    next: { revalidate: 600 },
  });
  const data = await res.json();
  return NextResponse.json(data, { status: res.status });
}
