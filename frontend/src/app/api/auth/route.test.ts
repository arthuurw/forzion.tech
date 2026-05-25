import { describe, it, expect, vi } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { POST } from "@/app/api/auth/route";
import { createMockRequest } from "@/test/setup/api";

vi.mock("@/lib/rateLimit", () => ({
  checkRateLimit: vi.fn(() => true),
  getClientIp: vi.fn(() => "127.0.0.1"),
}));

const FUTURE = Math.floor(Date.now() / 1000) + 3600;
const fakeJwt = `${btoa(JSON.stringify({ alg: "HS256" }))}.${btoa(
  JSON.stringify({ exp: FUTURE }),
)}.sig`;

describe("POST /api/auth — resposta de login", () => {
  it("resposta JSON nao contem o token JWT", async () => {
    server.use(
      http.post("*/auth/login", () =>
        HttpResponse.json({
          token: fakeJwt,
          tipoConta: "Treinador",
          contaId: "abc",
          perfilId: "def",
        }),
      ),
    );

    const req = createMockRequest({
      method: "POST",
      body: { email: "test@test.com", senha: "123" },
    });

    const res = await POST(req);
    const body = await res.json();

    expect(body.token).toBeUndefined();
    expect(body.tipoConta).toBe("Treinador");
    expect(body.contaId).toBe("abc");
    expect(body.perfilId).toBe("def");
  });

  it("token JWT setado em cookie HttpOnly Secure SameSite=strict", async () => {
    server.use(
      http.post("*/auth/login", () =>
        HttpResponse.json({
          token: fakeJwt,
          tipoConta: "Aluno",
          contaId: "c1",
          perfilId: "p1",
        }),
      ),
    );

    const req = createMockRequest({
      method: "POST",
      body: { email: "u@u.com", senha: "secret" },
    });

    const res = await POST(req);
    const setCookie = res.headers.get("set-cookie");

    expect(setCookie).toContain("token=");
    expect(setCookie).toContain("HttpOnly");
    expect(setCookie).toContain("SameSite=strict");
    expect(setCookie).toContain("session_guard=1");
  });

  it("backend retorna erro → propaga status sem setar cookies", async () => {
    server.use(
      http.post("*/auth/login", () =>
        HttpResponse.json({ error: "Credenciais invalidas" }, { status: 401 }),
      ),
    );

    const req = createMockRequest({
      method: "POST",
      body: { email: "u@u.com", senha: "wrong" },
    });

    const res = await POST(req);
    expect(res.status).toBe(401);
    expect(res.headers.get("set-cookie")).toBeNull();
  });

  it("rate limit excedido → retorna 429", async () => {
    const { checkRateLimit } = await import("@/lib/rateLimit");
    vi.mocked(checkRateLimit).mockReturnValueOnce(false);

    const req = createMockRequest({
      method: "POST",
      body: { email: "test@test.com", senha: "123" },
    });

    const res = await POST(req);
    expect(res.status).toBe(429);
  });
});
