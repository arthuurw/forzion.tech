import { isIP } from "node:net";
import type { NextRequest } from "next/server";
import { getClientIp } from "@/lib/rateLimit";

export function forwardedForHeader(request: NextRequest): Record<string, string> {
  const ip = getClientIp(request);
  if (isIP(ip) === 0) return {};
  return { "X-Forwarded-For": ip };
}
