import { describe, it, expect } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { GET } from "@/app/api/auth/treinadores/route";

describe("GET /api/auth/treinadores", () => {
  it("retorna lista do backend", async () => {
    const treinadores = [
      { id: "t1", nome: "Treinador 1" },
      { id: "t2", nome: "Treinador 2" },
    ];
    server.use(
      http.get("*/auth/treinadores", () => HttpResponse.json(treinadores)),
    );

    const res = await GET();
    expect(res.status).toBe(200);
    expect(await res.json()).toEqual(treinadores);
  });

  it("backend retorna 500 → propaga status", async () => {
    server.use(
      http.get("*/auth/treinadores", () =>
        HttpResponse.json({ error: "internal" }, { status: 500 }),
      ),
    );

    const res = await GET();
    expect(res.status).toBe(500);
  });
});
