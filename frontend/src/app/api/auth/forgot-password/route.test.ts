// Frontend proxy /api/auth/forgot-password → backend POST /auth/forgot-password.
// Spec: backend SEMPRE 200 (anti-enumeração). Cobre passthrough + erros raros
// (429 rate limit, JSON malformado).
import { describe, it, expect } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { POST } from "@/app/api/auth/forgot-password/route";
import { createMockRequest } from "@/test/setup/api";

describe("POST /api/auth/forgot-password", () => {
  it("email valido → repassa body, retorna 200", async () => {
    let receivedBody: unknown = null;
    server.use(
      http.post("*/auth/forgot-password", async ({ request }) => {
        receivedBody = await request.json();
        return HttpResponse.json({}, { status: 200 });
      }),
    );

    const req = createMockRequest({
      method: "POST",
      body: { email: "user@forzion.tech" },
    });

    const res = await POST(req);
    expect(res.status).toBe(200);
    expect(receivedBody).toEqual({ email: "user@forzion.tech" });
  });

  it("email inexistente → backend retorna 200 (anti-enumeração) → propaga 200", async () => {
    server.use(
      http.post("*/auth/forgot-password", () => HttpResponse.json({}, { status: 200 })),
    );

    const req = createMockRequest({
      method: "POST",
      body: { email: "naoexiste@forzion.tech" },
    });

    const res = await POST(req);
    expect(res.status).toBe(200);
  });

  it("backend 429 rate limit → propaga status", async () => {
    server.use(
      http.post("*/auth/forgot-password", () =>
        HttpResponse.json(
          { status: 429, title: "Too Many Requests" },
          { status: 429 },
        ),
      ),
    );

    const req = createMockRequest({
      method: "POST",
      body: { email: "user@forzion.tech" },
    });

    const res = await POST(req);
    expect(res.status).toBe(429);
  });

  it("JSON malformado → 400 sem chamar backend", async () => {
    let backendCalled = false;
    server.use(
      http.post("*/auth/forgot-password", () => {
        backendCalled = true;
        return HttpResponse.json({}, { status: 200 });
      }),
    );

    const req = createMockRequest({ method: "POST", body: {} });
    Object.defineProperty(req, "json", {
      value: async () => { throw new SyntaxError("Unexpected token"); },
    });

    const res = await POST(req);
    expect(res.status).toBe(400);
    expect(backendCalled).toBe(false);
  });

  it("emite X-Forwarded-For com o IP real, ignorando o header forjado pelo cliente", async () => {
    let received: string | null = null;
    server.use(
      http.post("*/auth/forgot-password", ({ request }) => {
        received = request.headers.get("x-forwarded-for");
        return HttpResponse.json({}, { status: 200 });
      }),
    );

    const req = createMockRequest({
      method: "POST",
      headers: { "x-real-ip": "203.0.113.7", "x-forwarded-for": "198.51.100.9" },
      body: { email: "user@forzion.tech" },
    });
    await POST(req);

    expect(received).toBe("203.0.113.7");
  });

  it("cross-origin → 403 sem chamar o backend", async () => {
    let backendCalled = false;
    server.use(
      http.post("*/auth/forgot-password", () => {
        backendCalled = true;
        return HttpResponse.json({}, { status: 200 });
      }),
    );

    const req = createMockRequest({
      method: "POST",
      headers: { origin: "http://evil.com" },
      body: { email: "user@forzion.tech" },
    });
    const res = await POST(req);
    const body = await res.json();

    expect(res.status).toBe(403);
    expect(body).toEqual({ error: "cross-origin" });
    expect(backendCalled).toBe(false);
  });
});
