import { describe, it, expect, beforeEach } from "vitest";
import { checkRateLimit, getClientIp, __resetRateLimit } from "@/lib/rateLimit";

describe("getClientIp", () => {
  function makeReq(data: Record<string, string>) {
    return { headers: { get: (name: string) => data[name] ?? null } };
  }

  it("usa x-real-ip quando presente", () => {
    expect(getClientIp(makeReq({ "x-real-ip": "1.2.3.4", "x-forwarded-for": "9.9.9.9" }) as never)).toBe("1.2.3.4");
  });

  it("usa PRIMEIRO IP de x-forwarded-for (cliente original, não último proxy)", () => {
    // Spoofing-resistant: atacante envia "evil, real-proxy" — pegamos o real-proxy injetado
    // pelo edge, não o "evil" controlado pelo cliente. Usar o último daria match com "evil".
    expect(getClientIp(makeReq({ "x-forwarded-for": "5.6.7.8, 10.10.10.10" }) as never)).toBe("5.6.7.8");
  });

  it("usa x-forwarded-for com um único IP", () => {
    expect(getClientIp(makeReq({ "x-forwarded-for": "1.1.1.1" }) as never)).toBe("1.1.1.1");
  });

  it("retorna 'unknown' quando nenhum header presente", () => {
    expect(getClientIp(makeReq({}) as never)).toBe("unknown");
  });
});

describe("checkRateLimit", () => {
  beforeEach(() => {
    __resetRateLimit();
  });

  it("primeiros 10 requests do mesmo IP → todos permitidos", () => {
    for (let i = 0; i < 10; i++) {
      expect(checkRateLimit("1.1.1.1")).toBe(true);
    }
  });

  it("11º request do mesmo IP na mesma janela → bloqueado", () => {
    for (let i = 0; i < 10; i++) checkRateLimit("1.1.1.1");
    expect(checkRateLimit("1.1.1.1")).toBe(false);
  });

  it("IPs diferentes não interferem entre si", () => {
    for (let i = 0; i < 10; i++) checkRateLimit("1.1.1.1");
    expect(checkRateLimit("1.1.1.1")).toBe(false);
    expect(checkRateLimit("2.2.2.2")).toBe(true);
  });
});
