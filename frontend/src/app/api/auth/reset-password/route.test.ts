import { describe, it, expect } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { POST } from "@/app/api/auth/reset-password/route";
import { createMockRequest } from "@/test/setup/api";

describe("POST /api/auth/reset-password", () => {
  it("token + senha valida → repassa body, retorna 200", async () => {
    let receivedBody: unknown = null;
    server.use(
      http.post("*/auth/reset-password", async ({ request }) => {
        receivedBody = await request.json();
        return HttpResponse.json({}, { status: 200 });
      }),
    );

    const req = createMockRequest({
      method: "POST",
      body: { token: "a".repeat(64), novaSenha: "SenhaForte123" },
    });

    const res = await POST(req);
    expect(res.status).toBe(200);
    expect(receivedBody).toEqual({
      token: "a".repeat(64),
      novaSenha: "SenhaForte123",
    });
  });

  it("backend 422 token expirado → propaga status + detail", async () => {
    const problem = {
      status: 422,
      title: "Erro de domínio",
      detail: "Token de redefinição expirado.",
    };
    server.use(
      http.post("*/auth/reset-password", () => HttpResponse.json(problem, { status: 422 })),
    );

    const req = createMockRequest({
      method: "POST",
      body: { token: "expired", novaSenha: "x" },
    });

    const res = await POST(req);
    expect(res.status).toBe(422);
    expect(await res.json()).toEqual(problem);
  });

  it("backend 400 senha fraca → propaga ValidationProblemDetails", async () => {
    const problem = {
      status: 400,
      title: "Erro de validação",
      errors: { novaSenha: ["Senha deve conter ao menos uma letra maiúscula."] },
    };
    server.use(
      http.post("*/auth/reset-password", () => HttpResponse.json(problem, { status: 400 })),
    );

    const req = createMockRequest({
      method: "POST",
      body: { token: "ok", novaSenha: "fraca" },
    });

    const res = await POST(req);
    expect(res.status).toBe(400);
    expect(await res.json()).toEqual(problem);
  });

  it("JSON malformado → 400 sem chamar backend", async () => {
    let backendCalled = false;
    server.use(
      http.post("*/auth/reset-password", () => {
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
});
