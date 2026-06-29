import { describe, it, expect, vi, afterEach } from "vitest";
import { extractTipoConta } from "@/lib/auth/jwt";
import { homeRouteFor } from "@/lib/auth/context";

function makeJwt(payload: Record<string, unknown>): string {
  const header = btoa(JSON.stringify({ alg: "HS256", typ: "JWT" }));
  const body = btoa(JSON.stringify(payload));
  return `${header}.${body}.fakesig`;
}

const FUTURE = Math.floor(Date.now() / 1000) + 3600;
const PAST = Math.floor(Date.now() / 1000) - 3600;

describe("extractTipoConta", () => {
  afterEach(() => vi.restoreAllMocks());

  it("retorna TipoConta para token válido", () => {
    const token = makeJwt({ tipo_conta: "Treinador", exp: FUTURE });
    expect(extractTipoConta(token)).toBe("Treinador");
  });

  it("retorna null para token expirado", () => {
    const token = makeJwt({ tipo_conta: "Aluno", exp: PAST });
    expect(extractTipoConta(token)).toBeNull();
  });

  it("retorna null para token malformado (< 3 partes)", () => {
    expect(extractTipoConta("aaa.bbb")).toBeNull();
  });

  it("retorna null para token sem tipo_conta", () => {
    const token = makeJwt({ exp: FUTURE });
    expect(extractTipoConta(token)).toBeNull();
  });

  it("retorna TipoConta sem exp (sem expiração)", () => {
    const token = makeJwt({ tipo_conta: "SystemAdmin" });
    expect(extractTipoConta(token)).toBe("SystemAdmin");
  });
});

describe("homeRouteFor", () => {
  it("Aluno → /aluno", () => {
    expect(homeRouteFor("Aluno")).toBe("/aluno");
  });
  it("Treinador → /treinador", () => {
    expect(homeRouteFor("Treinador")).toBe("/treinador");
  });
  it("SystemAdmin → /admin", () => {
    expect(homeRouteFor("SystemAdmin")).toBe("/admin");
  });
});
