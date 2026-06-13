import { describe, it, expect } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { POST } from "@/app/api/auth/refresh/route";
import { createMockRequest, extractCookies } from "@/test/setup/api";

const FUTURE = Math.floor(Date.now() / 1000) + 900;
const fakeJwt = `${btoa(JSON.stringify({ alg: "HS256" }))}.${btoa(
  JSON.stringify({ exp: FUTURE }),
)}.sig`;

describe("POST /api/auth/refresh", () => {
  it("sem cookie refresh → 401 limpando sessão", async () => {
    const res = await POST(createMockRequest({ method: "POST" }));
    expect(res.status).toBe(401);
    const setCookie = res.headers.get("set-cookie") ?? "";
    expect(setCookie).toContain("token=;");
    expect(setCookie).toContain("refresh=;");
  });

  it("refresh válido → rotaciona cookies e não vaza refresh no corpo", async () => {
    let cookieRecebido: string | null = null;
    server.use(
      http.post("*/auth/refresh", ({ request }) => {
        cookieRecebido = request.headers.get("cookie");
        return HttpResponse.json({
          token: fakeJwt,
          refreshToken: "refresh-rotacionado",
          tipoConta: "Aluno",
          contaId: "c1",
          perfilId: "p1",
          nome: "João",
        });
      }),
    );

    const res = await POST(
      createMockRequest({ method: "POST", cookies: { refresh: "refresh-antigo" } }),
    );

    expect(res.status).toBe(200);
    // refresh raw enviado ao backend via cookie httpOnly, nunca body
    expect(cookieRecebido).toBe("refresh=refresh-antigo");

    const body = await res.json();
    expect(body.token).toBeUndefined();
    expect(body.refreshToken).toBeUndefined();
    expect(body.tipoConta).toBe("Aluno");

    const cookies = extractCookies(res);
    expect(cookies.token).toBeTruthy();
    expect(cookies.refresh).toBe("refresh-rotacionado");
    expect(cookies.tipo_conta).toBe("Aluno");

    const setCookie = res.headers.get("set-cookie") ?? "";
    expect(setCookie).toContain("HttpOnly");
  });

  it("backend 401 (reuse/expirado) → 401 limpando sessão", async () => {
    server.use(http.post("*/auth/refresh", () => new HttpResponse(null, { status: 401 })));

    const res = await POST(
      createMockRequest({ method: "POST", cookies: { refresh: "refresh-morto" } }),
    );

    expect(res.status).toBe(401);
    const setCookie = res.headers.get("set-cookie") ?? "";
    expect(setCookie).toContain("refresh=;");
    expect(setCookie).toContain("token=;");
  });
});
