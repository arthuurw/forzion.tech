import { describe, it, expect, vi } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { POST } from "@/app/api/auth/register/treinador/route";
import { createMockRequest } from "@/test/setup/api";

vi.mock("@/lib/rateLimit", () => ({
  checkRateLimit: vi.fn(() => true),
  getClientIp: vi.fn(() => "127.0.0.1"),
}));

describe("POST /api/auth/register/treinador", () => {
  it("rate limit excedido → 429", async () => {
    const { checkRateLimit } = await import("@/lib/rateLimit");
    vi.mocked(checkRateLimit).mockReturnValueOnce(false);

    const req = createMockRequest({
      method: "POST",
      body: { nome: "X" },
    });
    const res = await POST(req);
    expect(res.status).toBe(429);
  });

  it("payload valido → propaga 201 com treinadorId", async () => {
    let receivedBody: unknown = null;
    server.use(
      http.post("*/auth/register/treinador", async ({ request }) => {
        receivedBody = await request.json();
        return HttpResponse.json({ treinadorId: "t-1" }, { status: 201 });
      }),
    );

    const payload = { nome: "Maria", email: "m@m.com", password: "abc12345" };
    const req = createMockRequest({ method: "POST", body: payload });
    const res = await POST(req);

    expect(res.status).toBe(201);
    expect(await res.json()).toEqual({ treinadorId: "t-1" });
    expect(receivedBody).toEqual(payload);
  });

  it("JSON malformado → 400 sem chamar backend", async () => {
    let backendCalled = false;
    server.use(
      http.post("*/auth/register/treinador", () => {
        backendCalled = true;
        return HttpResponse.json({}, { status: 201 });
      }),
    );

    const req = createMockRequest({ method: "POST", body: {} });
    Object.defineProperty(req, "json", {
      value: async () => {
        throw new SyntaxError("Unexpected token");
      },
    });

    const res = await POST(req);
    expect(res.status).toBe(400);
    expect(backendCalled).toBe(false);
    expect(await res.json()).toEqual({
      title: "Corpo da requisição inválido.",
      status: 400,
    });
  });

  it("backend 422 → propaga status e mensagem", async () => {
    server.use(
      http.post("*/auth/register/treinador", () =>
        HttpResponse.json({ error: "validacao" }, { status: 422 }),
      ),
    );

    const req = createMockRequest({ method: "POST", body: {} });
    const res = await POST(req);
    expect(res.status).toBe(422);
  });
});
