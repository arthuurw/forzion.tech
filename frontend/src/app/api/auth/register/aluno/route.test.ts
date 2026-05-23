import { describe, it, expect, vi } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { POST } from "@/app/api/auth/register/aluno/route";
import { createMockRequest } from "@/test/setup/api";

vi.mock("@/lib/rateLimit", () => ({
  checkRateLimit: vi.fn(() => true),
  getClientIp: vi.fn(() => "127.0.0.1"),
}));

describe("POST /api/auth/register/aluno", () => {
  it("rate limit excedido → 429", async () => {
    const { checkRateLimit } = await import("@/lib/rateLimit");
    vi.mocked(checkRateLimit).mockReturnValueOnce(false);

    const req = createMockRequest({
      method: "POST",
      body: { nome: "X", email: "x@x.com" },
    });
    const res = await POST(req);
    expect(res.status).toBe(429);
  });

  it("payload valido → propaga 201 do backend com body", async () => {
    let receivedBody: unknown = null;
    server.use(
      http.post("*/auth/register/aluno", async ({ request }) => {
        receivedBody = await request.json();
        return HttpResponse.json({ alunoId: "novo-id" }, { status: 201 });
      }),
    );

    const payload = { nome: "Joao", email: "joao@x.com", password: "abc12345" };
    const req = createMockRequest({ method: "POST", body: payload });
    const res = await POST(req);

    expect(res.status).toBe(201);
    expect(await res.json()).toEqual({ alunoId: "novo-id" });
    expect(receivedBody).toEqual(payload);
  });

  it("backend retorna 400 → propaga status e body", async () => {
    server.use(
      http.post("*/auth/register/aluno", () =>
        HttpResponse.json({ error: "email ja existe" }, { status: 400 }),
      ),
    );

    const req = createMockRequest({ method: "POST", body: { email: "x@x.com" } });
    const res = await POST(req);

    expect(res.status).toBe(400);
    expect(await res.json()).toEqual({ error: "email ja existe" });
  });
});
