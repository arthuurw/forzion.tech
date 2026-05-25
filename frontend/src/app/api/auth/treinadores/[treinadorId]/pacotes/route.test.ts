import { describe, it, expect } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { GET } from "@/app/api/auth/treinadores/[treinadorId]/pacotes/route";
import { createMockRequest } from "@/test/setup/api";

function makeCtx(treinadorId: string) {
  return { params: Promise.resolve({ treinadorId }) };
}

describe("GET /api/auth/treinadores/[treinadorId]/pacotes", () => {
  it("repassa treinadorId na URL e retorna pacotes", async () => {
    let receivedUrl: URL | null = null;
    const pacotes = [{ id: "p1", nome: "Pacote Basico" }];

    server.use(
      http.get("*/auth/treinadores/:id/pacotes", ({ request }) => {
        receivedUrl = new URL(request.url);
        return HttpResponse.json(pacotes);
      }),
    );

    const req = createMockRequest({ method: "GET" });
    const res = await GET(req, makeCtx("t-abc"));

    expect(res.status).toBe(200);
    expect(await res.json()).toEqual(pacotes);
    expect((receivedUrl as unknown as URL).pathname).toContain("/auth/treinadores/t-abc/pacotes");
  });

  it("backend 404 → propaga status", async () => {
    server.use(
      http.get("*/auth/treinadores/:id/pacotes", () =>
        HttpResponse.json({ error: "not found" }, { status: 404 }),
      ),
    );

    const req = createMockRequest({ method: "GET" });
    const res = await GET(req, makeCtx("inexistente"));
    expect(res.status).toBe(404);
  });
});
