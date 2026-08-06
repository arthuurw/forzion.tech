import { NextResponse, type NextRequest } from "next/server";
import { cookies } from "next/headers";
import { clearSessionCookies } from "@/lib/auth/sessionCookies";
import { isCrossOrigin } from "@/lib/security/sameOrigin";

const API_BASE = process.env.API_BASE_URL ?? "https://localhost:7220";

export async function POST(request: NextRequest) {
  if (isCrossOrigin(request)) {
    return NextResponse.json({ error: "cross-origin" }, { status: 403 });
  }

  const cookieStore = await cookies();
  const token = cookieStore.get("token")?.value;

  if (token) {
    try {
      await fetch(`${API_BASE}/conta/logout`, {
        method: "POST",
        headers: { Authorization: `Bearer ${token}` },
      });
    } catch {
      // falha silenciosa — cookies são limpos de qualquer forma
    }
  }

  const response = NextResponse.json({ ok: true });
  clearSessionCookies(response);
  return response;
}
