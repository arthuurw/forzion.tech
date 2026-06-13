import { describe, it, expect, vi, afterEach } from "vitest";
import { NextResponse } from "next/server";
import {
  accessTokenMaxAge,
  refreshMaxAge,
  baseCookieOpts,
  applySessionCookies,
  clearSessionCookies,
  fetchBackendRefresh,
} from "./sessionCookies";
import type { RefreshResponse } from "@/types";

function jwtComExp(exp: number): string {
  const header = btoa(JSON.stringify({ alg: "HS256" }));
  const payload = btoa(JSON.stringify({ exp }));
  return `${header}.${payload}.sig`;
}

function setCookieHeader(res: NextResponse, name: string): string {
  const all = res.headers.getSetCookie();
  const found = all.find((c) => c.startsWith(`${name}=`));
  if (!found) throw new Error(`cookie ${name} não setado`);
  return found;
}

afterEach(() => vi.unstubAllGlobals());

describe("accessTokenMaxAge", () => {
  it("deriva segundos restantes do exp do JWT", () => {
    const exp = Math.floor(Date.now() / 1000) + 900;
    expect(accessTokenMaxAge(jwtComExp(exp))).toBeGreaterThan(880);
    expect(accessTokenMaxAge(jwtComExp(exp))).toBeLessThanOrEqual(900);
  });

  it("JWT malformado → undefined (cookie de sessão)", () => {
    expect(accessTokenMaxAge("não.é.jwt")).toBeUndefined();
  });

  it("JWT sem exp → undefined", () => {
    const noExp = `${btoa(JSON.stringify({ alg: "HS256" }))}.${btoa(JSON.stringify({ foo: 1 }))}.sig`;
    expect(accessTokenMaxAge(noExp)).toBeUndefined();
  });
});

describe("refreshMaxAge", () => {
  it("Admin → janela curta (2h)", () => {
    expect(refreshMaxAge("SystemAdmin")).toBe(2 * 60 * 60);
  });

  it("Aluno/Treinador → 7d", () => {
    expect(refreshMaxAge("Aluno")).toBe(7 * 24 * 60 * 60);
    expect(refreshMaxAge("Treinador")).toBe(7 * 24 * 60 * 60);
  });

  it("papel desconhecido ou undefined → default 7d", () => {
    expect(refreshMaxAge("Hacker")).toBe(7 * 24 * 60 * 60);
    expect(refreshMaxAge(undefined)).toBe(7 * 24 * 60 * 60);
  });
});

describe("baseCookieOpts", () => {
  it("SameSite=strict, path=/, secure só em produção", () => {
    const opts = baseCookieOpts(100);
    expect(opts.sameSite).toBe("strict");
    expect(opts.path).toBe("/");
    expect(opts.maxAge).toBe(100);
    expect(opts.secure).toBe(process.env.NODE_ENV === "production");
  });
});

describe("applySessionCookies", () => {
  it("seta token/refresh httpOnly e tipo_conta NÃO-httpOnly", () => {
    const res = NextResponse.json({});
    applySessionCookies(res, {
      token: jwtComExp(Math.floor(Date.now() / 1000) + 900),
      refreshToken: "raw-refresh-abc",
      tipoConta: "Aluno",
    });

    expect(res.cookies.get("token")?.value).toBeTruthy();
    expect(res.cookies.get("refresh")?.value).toBe("raw-refresh-abc");
    expect(res.cookies.get("tipo_conta")?.value).toBe("Aluno");

    expect(setCookieHeader(res, "token")).toContain("HttpOnly");
    expect(setCookieHeader(res, "refresh")).toContain("HttpOnly");
    // hint de roteamento é legível pelo middleware → NÃO httpOnly
    expect(setCookieHeader(res, "tipo_conta")).not.toContain("HttpOnly");
  });
});

describe("clearSessionCookies", () => {
  it("expira os 4 cookies de sessão", () => {
    const res = NextResponse.json({});
    clearSessionCookies(res);
    for (const name of ["token", "refresh", "session_guard", "tipo_conta"]) {
      expect(setCookieHeader(res, name)).toContain("Expires=Thu, 01 Jan 1970");
    }
  });
});

describe("fetchBackendRefresh", () => {
  const par: RefreshResponse = {
    token: "novo.access",
    refreshToken: "novo-refresh",
    tipoConta: "Aluno",
    contaId: "c1",
    perfilId: "p1",
    nome: "João",
  };

  it("repassa refresh raw via cookie e retorna o par rotacionado", async () => {
    const fetchMock = vi.fn(
      async (_url: string, _init?: RequestInit) => new Response(JSON.stringify(par), { status: 200 }),
    );
    vi.stubGlobal("fetch", fetchMock);

    const result = await fetchBackendRefresh("raw-123");

    expect(result).toEqual(par);
    const init = fetchMock.mock.calls[0][1];
    expect(init?.headers).toMatchObject({ Cookie: "refresh=raw-123" });
  });

  it("backend 401 → null (sessão morta)", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(null, { status: 401 })));
    expect(await fetchBackendRefresh("raw")).toBeNull();
  });

  it("erro de rede → null", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => { throw new Error("network"); }));
    expect(await fetchBackendRefresh("raw")).toBeNull();
  });
});
