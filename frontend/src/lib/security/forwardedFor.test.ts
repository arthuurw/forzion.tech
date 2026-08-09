import { describe, it, expect } from "vitest";
import type { NextRequest } from "next/server";
import { forwardedForHeader } from "./forwardedFor";

function req(headers: Record<string, string>): NextRequest {
  return { headers: new Headers(headers) } as unknown as NextRequest;
}

describe("forwardedForHeader", () => {
  it("X-Real-IP presente → emite X-Forwarded-For com esse IP", () => {
    expect(forwardedForHeader(req({ "x-real-ip": "203.0.113.7" }))).toEqual({
      "X-Forwarded-For": "203.0.113.7",
    });
  });

  it("X-Real-IP tem precedência sobre o X-Forwarded-For enviado pelo cliente", () => {
    const headers = forwardedForHeader(
      req({ "x-real-ip": "203.0.113.7", "x-forwarded-for": "198.51.100.9" }),
    );
    expect(headers).toEqual({ "X-Forwarded-For": "203.0.113.7" });
  });

  it("sem X-Real-IP → usa o primeiro hop de X-Forwarded-For, nunca o último", () => {
    expect(
      forwardedForHeader(req({ "x-forwarded-for": "203.0.113.7, 198.51.100.9" })),
    ).toEqual({ "X-Forwarded-For": "203.0.113.7" });
  });

  it("IPv6 é emitido", () => {
    expect(forwardedForHeader(req({ "x-real-ip": "2001:db8::1" }))).toEqual({
      "X-Forwarded-For": "2001:db8::1",
    });
  });

  it("sem nenhum header de IP → não emite o header", () => {
    expect(forwardedForHeader(req({}))).toEqual({});
  });

  it("IP resolvido que não parseia como IPv4/IPv6 → não emite o header", () => {
    expect(forwardedForHeader(req({ "x-real-ip": "evil" }))).toEqual({});
  });

  it("primeiro hop de XFF inválido → não emite o header", () => {
    expect(
      forwardedForHeader(req({ "x-forwarded-for": "unknown, 203.0.113.7" })),
    ).toEqual({});
  });
});
