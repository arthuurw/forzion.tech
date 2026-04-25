import { describe, it, expect } from "vitest";
import { GET } from "@/app/api/auth/me/route";
import type { NextRequest } from "next/server";

function makeJwt(payload: Record<string, unknown>): string {
  const header = btoa(JSON.stringify({ alg: "HS256", typ: "JWT" }));
  const body = btoa(JSON.stringify(payload));
  return `${header}.${body}.fakesig`;
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
    const token = makeJwt({ conta_id: "1", tipo_conta: "Aluno", perfil_id: "2", exp: FUTURE });
    const res = await GET(makeRequest({ token }));
    expect(await res.json()).toBeNull();
  });

  it("token expirado → retorna null", async () => {
    const token = makeJwt({ conta_id: "1", tipo_conta: "Aluno", perfil_id: "2", exp: PAST });
    const res = await GET(makeRequest({ token, session_guard: "1" }));
    expect(await res.json()).toBeNull();
  });

  it("token sem perfil_id → retorna null", async () => {
    const token = makeJwt({ conta_id: "1", tipo_conta: "Aluno", exp: FUTURE });
    const res = await GET(makeRequest({ token, session_guard: "1" }));
    expect(await res.json()).toBeNull();
  });

  it("token válido → retorna SessionUser correto", async () => {
    const token = makeJwt({ conta_id: "abc", tipo_conta: "Treinador", perfil_id: "def", exp: FUTURE });
    const res = await GET(makeRequest({ token, session_guard: "1" }));
    const user = await res.json();
    expect(user).not.toBeNull();
    expect(user.contaId).toBe("abc");
    expect(user.tipoConta).toBe("Treinador");
    expect(user.perfilId).toBe("def");
    expect(user.token).toBe(token);
  });
});
