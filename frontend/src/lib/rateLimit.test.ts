import { describe, it, expect, beforeEach } from "vitest";
import { checkRateLimit, getClientIp } from "@/lib/rateLimit";

// getClientIp

describe("getClientIp", () => {
  function makeReq(data: Record<string, string>) {
    return { headers: { get: (name: string) => data[name] ?? null } };
  }

  it("usa x-real-ip quando presente", () => {
    expect(getClientIp(makeReq({ "x-real-ip": "1.2.3.4", "x-forwarded-for": "9.9.9.9" }) as never)).toBe("1.2.3.4");
  });

  it("usa último IP de x-forwarded-for quando x-real-ip ausente", () => {
    expect(getClientIp(makeReq({ "x-forwarded-for": "5.6.7.8, 10.10.10.10" }) as never)).toBe("10.10.10.10");
  });

  it("usa x-forwarded-for com um único IP", () => {
    expect(getClientIp(makeReq({ "x-forwarded-for": "1.1.1.1" }) as never)).toBe("1.1.1.1");
  });

  it("retorna 'unknown' quando nenhum header presente", () => {
    expect(getClientIp(makeReq({}) as never)).toBe("unknown");
  });
});

// checkRateLimit

describe("checkRateLimit", () => {
  beforeEach(() => {
    // Force reset the module-level map by using different IPs per test
  });

  it("primeiros 10 requests do mesmo IP → todos permitidos", () => {
    const ip = `test-ip-${Date.now()}-allow`;
    for (let i = 0; i < 10; i++) {
      expect(checkRateLimit(ip)).toBe(true);
    }
  });

  it("11º request do mesmo IP na mesma janela → bloqueado", () => {
    const ip = `test-ip-${Date.now()}-block`;
    for (let i = 0; i < 10; i++) checkRateLimit(ip);
    expect(checkRateLimit(ip)).toBe(false);
  });

  it("IPs diferentes não interferem entre si", () => {
    const ip1 = `test-ip-${Date.now()}-a`;
    const ip2 = `test-ip-${Date.now()}-b`;
    for (let i = 0; i < 10; i++) checkRateLimit(ip1);
    expect(checkRateLimit(ip1)).toBe(false);
    expect(checkRateLimit(ip2)).toBe(true);
  });
});
