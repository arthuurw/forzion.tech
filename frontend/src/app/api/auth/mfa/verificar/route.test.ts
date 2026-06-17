import { describe, it, expect, vi } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { POST } from "@/app/api/auth/mfa/verificar/route";
import { createMockRequest, extractCookies } from "@/test/setup/api";

vi.mock("@/lib/rateLimit", () => ({
  checkRateLimit: vi.fn(() => true),
  getClientIp: vi.fn(() => "127.0.0.1"),
}));

const FUTURE = Math.floor(Date.now() / 1000) + 3600;
const fakeJwt = `${btoa(JSON.stringify({ alg: "HS256" }))}.${btoa(JSON.stringify({ exp: FUTURE }))}.sig`;

function loginPayload(trustedDeviceToken: string | null) {
  return {
    login: {
      token: fakeJwt,
      refreshToken: "raw-refresh",
      tipoConta: "Aluno",
      contaId: "c1",
      perfilId: "p1",
      nome: "Maria",
    },
    trustedDeviceToken,
  };
}

describe("POST /api/auth/mfa/verificar", () => {
  it("sem cookie mfa_pending → 401 sem chamar o backend", async () => {
    let chamado = false;
    server.use(
      http.post("*/auth/mfa/verificar", () => {
        chamado = true;
        return HttpResponse.json(loginPayload(null));
      }),
    );

    const req = createMockRequest({ method: "POST", body: { codigo: "123456", fator: 0, lembrarDispositivo: false } });
    const res = await POST(req);

    expect(res.status).toBe(401);
    expect(chamado).toBe(false);
  });

  it("encaminha o token pendente como Bearer e seta a sessão sem expor o token", async () => {
    let authHeader: string | null = null;
    server.use(
      http.post("*/auth/mfa/verificar", ({ request }) => {
        authHeader = request.headers.get("Authorization");
        return HttpResponse.json(loginPayload(null));
      }),
    );

    const req = createMockRequest({
      method: "POST",
      cookies: { mfa_pending: "pending-tok" },
      body: { codigo: "123456", fator: 0, lembrarDispositivo: false },
    });
    const res = await POST(req);
    const body = await res.json();
    const cookies = extractCookies(res);

    expect(authHeader).toBe("Bearer pending-tok");
    expect(body.token).toBeUndefined();
    expect(body.contaId).toBe("c1");
    expect(cookies.refresh).toBe("raw-refresh");
    expect(cookies.tipo_conta).toBe("Aluno");
    expect(cookies.mfa_pending).toBe("");
    expect(cookies.trusted_device).toBeUndefined();
  });

  it("com lembrarDispositivo → seta cookie trusted_device httpOnly", async () => {
    server.use(http.post("*/auth/mfa/verificar", () => HttpResponse.json(loginPayload("device-raw"))));

    const req = createMockRequest({
      method: "POST",
      cookies: { mfa_pending: "pending-tok" },
      body: { codigo: "123456", fator: 0, lembrarDispositivo: true },
    });
    const res = await POST(req);
    const cookies = extractCookies(res);

    expect(cookies.trusted_device).toBe("device-raw");
    const setCookie = res.headers.getSetCookie().find((c) => c.startsWith("trusted_device="));
    expect(setCookie).toContain("HttpOnly");
  });

  it("backend recusa o código → propaga status sem setar sessão", async () => {
    server.use(
      http.post("*/auth/mfa/verificar", () =>
        HttpResponse.json({ detail: "Código inválido." }, { status: 422 }),
      ),
    );

    const req = createMockRequest({
      method: "POST",
      cookies: { mfa_pending: "pending-tok" },
      body: { codigo: "000000", fator: 0, lembrarDispositivo: false },
    });
    const res = await POST(req);

    expect(res.status).toBe(422);
    const cookies = extractCookies(res);
    expect(cookies.token).toBeUndefined();
  });
});
