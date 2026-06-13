import { describe, it, expect, beforeAll } from "vitest";
import { SignJWT } from "jose";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { GET } from "@/app/api/auth/me/route";
import { extractCookies } from "@/test/setup/api";
import type { NextRequest } from "next/server";

const TEST_SECRET = "test-secret-minimo-32-caracteres-ok";

beforeAll(() => {
  process.env.JWT_SECRET = TEST_SECRET;
  delete process.env.JWT_ISSUER;
  delete process.env.JWT_AUDIENCE;
});

async function makeJwt(payload: Record<string, unknown>): Promise<string> {
  const secret = new TextEncoder().encode(TEST_SECRET);
  const builder = new SignJWT(payload as Record<string, unknown>).setProtectedHeader({ alg: "HS256" });
  if (typeof payload.exp === "number") {
    builder.setExpirationTime(payload.exp);
  }
  return builder.sign(secret);
}

const FUTURE = Math.floor(Date.now() / 1000) + 3600;
const PAST = Math.floor(Date.now() / 1000) - 3600;

function makeRequest(cookies: Record<string, string>): NextRequest {
  return {
    cookies: {
      get: (name: string) => {
        const val = cookies[name];
        return val !== undefined ? { name, value: val } : undefined;
      },
    },
  } as unknown as NextRequest;
}

describe("GET /api/auth/me", () => {
  it("sem cookies → retorna null", async () => {
    const res = await GET(makeRequest({}));
    expect(await res.json()).toBeNull();
  });

  it("sem session_guard → retorna null", async () => {
    const token = await makeJwt({ conta_id: "1", tipo_conta: "Aluno", perfil_id: "2", exp: FUTURE });
    const res = await GET(makeRequest({ token }));
    expect(await res.json()).toBeNull();
  });

  it("token expirado → retorna null", async () => {
    const token = await makeJwt({ conta_id: "1", tipo_conta: "Aluno", perfil_id: "2", exp: PAST });
    const res = await GET(makeRequest({ token, session_guard: "1" }));
    expect(await res.json()).toBeNull();
  });

  it("token sem perfil_id → retorna null", async () => {
    const token = await makeJwt({ conta_id: "1", tipo_conta: "Aluno", exp: FUTURE });
    const res = await GET(makeRequest({ token, session_guard: "1" }));
    expect(await res.json()).toBeNull();
  });

  it("token com assinatura inválida → retorna null", async () => {
    const validToken = await makeJwt({ conta_id: "abc", tipo_conta: "Treinador", perfil_id: "def", exp: FUTURE });
    const parts = validToken.split(".");
    const forged = `${parts[0]}.${parts[1]}.invalidsignature`;
    const res = await GET(makeRequest({ token: forged, session_guard: "1" }));
    expect(await res.json()).toBeNull();
  });

  it("token válido → retorna SessionUser correto sem expor o token", async () => {
    const token = await makeJwt({ conta_id: "abc", tipo_conta: "Treinador", perfil_id: "def", nome: "Carlos Silva", exp: FUTURE });
    const res = await GET(makeRequest({ token, session_guard: "1" }));
    const user = await res.json();
    expect(user).not.toBeNull();
    expect(user.contaId).toBe("abc");
    expect(user.tipoConta).toBe("Treinador");
    expect(user.perfilId).toBe("def");
    expect(user.nome).toBe("Carlos Silva");
    expect(user.token).toBeUndefined();
  });

  it("token sem claim nome (legado) → nome vazio, sessão preservada", async () => {
    const token = await makeJwt({ conta_id: "abc", tipo_conta: "Aluno", perfil_id: "def", exp: FUTURE });
    const res = await GET(makeRequest({ token, session_guard: "1" }));
    const user = await res.json();
    expect(user).not.toBeNull();
    expect(user.nome).toBe("");
  });
});

describe("GET /api/auth/me — renovação silenciosa", () => {
  const novoPar = {
    token: "novo.access.jwt",
    refreshToken: "novo-refresh",
    tipoConta: "Aluno",
    contaId: "c1",
    perfilId: "p1",
    nome: "Maria",
  };

  it("access expirado + refresh válido → rotaciona e devolve SessionUser", async () => {
    server.use(http.post("*/auth/refresh", () => HttpResponse.json(novoPar)));
    const token = await makeJwt({ conta_id: "c1", tipo_conta: "Aluno", perfil_id: "p1", exp: PAST });

    const res = await GET(makeRequest({ token, session_guard: "1", refresh: "raw-antigo" }));
    const user = await res.json();

    expect(user).not.toBeNull();
    expect(user.contaId).toBe("c1");
    expect(user.nome).toBe("Maria");
    const cookies = extractCookies(res);
    expect(cookies.refresh).toBe("novo-refresh");
    expect(cookies.tipo_conta).toBe("Aluno");
  });

  it("sem access mas refresh válido → renova mesmo sem token", async () => {
    server.use(http.post("*/auth/refresh", () => HttpResponse.json(novoPar)));
    const res = await GET(makeRequest({ refresh: "raw-antigo" }));
    const user = await res.json();
    expect(user).not.toBeNull();
    expect(user.contaId).toBe("c1");
  });

  it("refresh inválido (backend 401) → null limpando cookies", async () => {
    server.use(http.post("*/auth/refresh", () => new HttpResponse(null, { status: 401 })));
    const res = await GET(makeRequest({ refresh: "raw-morto" }));
    expect(await res.json()).toBeNull();
    const setCookie = res.headers.get("set-cookie") ?? "";
    expect(setCookie).toContain("refresh=;");
  });
});
