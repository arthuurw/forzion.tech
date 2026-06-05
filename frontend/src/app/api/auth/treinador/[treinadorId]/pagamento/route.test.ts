import { describe, it, expect, vi } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { POST } from "@/app/api/auth/treinador/[treinadorId]/pagamento/route";
import { createMockRequest } from "@/test/setup/api";

vi.mock("@/lib/rateLimit", () => ({
  checkRateLimit: vi.fn(() => true),
  getClientIp: vi.fn(() => "127.0.0.1"),
}));

const params = Promise.resolve({ treinadorId: "t-1" });

describe("POST /api/auth/treinador/[treinadorId]/pagamento", () => {
  it("rate limit excedido → 429", async () => {
    const { checkRateLimit } = await import("@/lib/rateLimit");
    vi.mocked(checkRateLimit).mockReturnValueOnce(false);

    const req = createMockRequest({ method: "POST", body: { metodo: "Pix" } });
    const res = await POST(req, { params });
    expect(res.status).toBe(429);
  });

  it("payload valido → propaga 200 com dados do pagamento e treinadorId na URL", async () => {
    let receivedBody: unknown = null;
    let receivedUrl = "";
    server.use(
      http.post("*/auth/treinador/:id/pagamento", async ({ request, params: p }) => {
        receivedBody = await request.json();
        receivedUrl = String(p.id);
        return HttpResponse.json({ pagamentoId: "pg-1", pixQrCode: "00020126" }, { status: 200 });
      }),
    );

    const req = createMockRequest({ method: "POST", body: { metodo: "Pix" } });
    const res = await POST(req, { params });

    expect(res.status).toBe(200);
    expect(await res.json()).toEqual({ pagamentoId: "pg-1", pixQrCode: "00020126" });
    expect(receivedBody).toEqual({ metodo: "Pix" });
    expect(receivedUrl).toBe("t-1");
  });

  it("JSON malformado → 400 sem chamar backend", async () => {
    let backendCalled = false;
    server.use(
      http.post("*/auth/treinador/:id/pagamento", () => {
        backendCalled = true;
        return HttpResponse.json({}, { status: 200 });
      }),
    );

    const req = createMockRequest({ method: "POST", body: {} });
    Object.defineProperty(req, "json", {
      value: async () => {
        throw new SyntaxError("Unexpected token");
      },
    });

    const res = await POST(req, { params });
    expect(res.status).toBe(400);
    expect(backendCalled).toBe(false);
  });

  it("backend 404 → propaga status", async () => {
    server.use(
      http.post("*/auth/treinador/:id/pagamento", () =>
        HttpResponse.json({ title: "nao encontrado" }, { status: 404 }),
      ),
    );

    const req = createMockRequest({ method: "POST", body: { metodo: "Cartao" } });
    const res = await POST(req, { params });
    expect(res.status).toBe(404);
  });
});
