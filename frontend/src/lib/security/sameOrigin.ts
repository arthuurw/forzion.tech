import type { NextRequest } from "next/server";

export function isCrossOrigin(request: NextRequest): boolean {
  const origin = request.headers.get("origin");
  if (!origin) return false;

  const host = request.headers.get("host");
  if (!host) return true;

  let originHost: string;
  try {
    originHost = new URL(origin).host;
  } catch {
    return true;
  }
  return originHost !== host;
}
