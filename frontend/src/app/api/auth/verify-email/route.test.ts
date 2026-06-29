import { describe, it, expect } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { POST } from "@/app/api/auth/verify-email/route";
import { createMockRequest } from "@/test/setup/api";

describe("POST /api/auth/verify-email", () => {
  it("token valido → repassa body pra backend, retorna 200 com {}", async () => {
    let receivedBody: unknown = null;
    server.use(
      http.post("*/auth/verify-email", async ({ request }) => {
        receivedBody = await request.json();
        return HttpResponse.json({}, { status: 200 });
      }),
    );

    const req = createMockRequest({
      method: "POST",
      body: { token: "a".repeat(64) },
    });

    const res = await POST(req);

    expect(res.status).toBe(200);
    expect(await res.json()).toEqual({});
    expect(receivedBody).toEqual({ token: "a".repeat(64) });
  });

  it("backend 422 token invalido → propaga status + ProblemDetails", async () => {
    const problem = {
      status: 422,
      title: "Erro de domínio",
      detail: "Token de verificação inválido ou já utilizado.",
    };
    server.use(
      http.post("*/auth/verify-email", () => HttpResponse.json(problem, { status: 422 })),
    );

    const req = createMockRequest({
      method: "POST",
      body: { token: "bad" },
    });

    const res = await POST(req);
    expect(res.status).toBe(422);
    expect(await res.json()).toEqual(problem);
  });

  it("backend 400 ValidationException → propaga", async () => {
    server.use(
      http.post("*/auth/verify-email", () =>
        HttpResponse.json({ status: 400, title: "Erro de validação" }, { status: 400 }),
      ),
    );

    const req = createMockRequest({ method: "POST", body: { token: "" } });
    const res = await POST(req);
    expect(res.status).toBe(400);
  });

  it("JSON malformado → 400 sem chamar backend", async () => {
    let backendCalled = false;
    server.use(
      http.post("*/auth/verify-email", () => {
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
    expect(await res.json()).toEqual({
      title: "Corpo da requisição inválido.",
      status: 400,
    });
  });
});
