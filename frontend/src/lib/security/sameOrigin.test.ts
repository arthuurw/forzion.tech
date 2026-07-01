import { describe, it, expect } from "vitest";
import type { NextRequest } from "next/server";
import { isCrossOrigin } from "./sameOrigin";

function req(origin: string | undefined, host = "localhost:3000"): NextRequest {
  const headers = new Headers();
  if (origin !== undefined) headers.set("origin", origin);
  return {
    headers,
    nextUrl: { origin: `http://${host}` },
  } as unknown as NextRequest;
}

describe("isCrossOrigin", () => {
  it("Origin de outro host → cross-origin", () => {
    expect(isCrossOrigin(req("http://evil.com"))).toBe(true);
  });

  it("Origin do mesmo host (protocolo diverge) → same-origin", () => {
    expect(isCrossOrigin(req("https://localhost:3000"))).toBe(false);
  });

  it("Origin ausente → fail-open (não é cross)", () => {
    expect(isCrossOrigin(req(undefined))).toBe(false);
  });

  it("Origin malformado → tratado como cross-origin", () => {
    expect(isCrossOrigin(req("not-a-url"))).toBe(true);
  });
});
