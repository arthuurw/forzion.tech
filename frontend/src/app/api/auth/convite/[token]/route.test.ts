import { describe, it, expect } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { GET } from "@/app/api/auth/convite/[token]/route";
import { createMockRequest } from "@/test/setup/api";

function makeCtx(token: string) {
  return { params: Promise.resolve({ token }) };
}

describe("GET /api/auth/convite/[token]", () => {
  it("repassa o token na URL e retorna os dados resolvidos", async () => {
    let receivedUrl: URL | null = null;
    const resolved = { nome: "Maria", contatoTipo: "Email", contatoValor: "maria@example.com", treinadorId: "t-1", treinadorNome: "Carlos" };

    server.use(
      http.get("*/auth/convite/:token", ({ request }) => {
        receivedUrl = new URL(request.url);
        return HttpResponse.json(resolved);
      }),
    );

    const req = createMockRequest({ method: "GET" });
    const res = await GET(req, makeCtx("abc123"));

    expect(res.status).toBe(200);
    expect(await res.json()).toEqual(resolved);
    expect((receivedUrl as unknown as URL).pathname).toContain("/auth/convite/abc123");
  });

  it("token inválido/expirado/consumido → propaga 404 genérico", async () => {
    server.use(
      http.get("*/auth/convite/:token", () =>
        HttpResponse.json({ title: "Not Found", status: 404 }, { status: 404 }),
      ),
    );

    const req = createMockRequest({ method: "GET" });
    const res = await GET(req, makeCtx("invalido"));
    expect(res.status).toBe(404);
  });
});
