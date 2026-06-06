import { describe, it, expect } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { GET } from "@/app/api/auth/planos/route";

describe("GET /api/auth/planos", () => {
  it("retorna lista de planos do backend", async () => {
    const planos = [
      { planoId: "p1", nome: "Basic", tier: "Basic", preco: 50, maxAlunos: 20 },
      { planoId: "p2", nome: "Pro", tier: "Pro", preco: 120, maxAlunos: 50 },
    ];
    server.use(http.get("*/auth/planos", () => HttpResponse.json(planos)));

    const res = await GET();
    expect(res.status).toBe(200);
    expect(await res.json()).toEqual(planos);
  });

  it("backend retorna 500 → propaga status", async () => {
    server.use(
      http.get("*/auth/planos", () =>
        HttpResponse.json({ error: "internal" }, { status: 500 }),
      ),
    );

    const res = await GET();
    expect(res.status).toBe(500);
  });
});
