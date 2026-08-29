import { describe, it, expect } from "vitest";
import type { NextRequest } from "next/server";
import { withSameOrigin } from "./withSameOrigin";

function req(origin: string | undefined, host: string | null = "localhost:3000"): NextRequest {
  const headers = new Headers();
  if (origin !== undefined) headers.set("origin", origin);
  if (host !== null) headers.set("host", host);
  return { headers } as unknown as NextRequest;
}

describe("withSameOrigin", () => {
  it("same-origin → delega para o handler e devolve a resposta dele", async () => {
    const handler = withSameOrigin(async () => Response.json({ ok: true }));

    const res = await handler(req("https://localhost:3000"));

    expect(res.status).toBe(200);
    expect(await res.json()).toEqual({ ok: true });
  });

  it("cross-origin → responde 403 sem chamar o handler", async () => {
    let called = false;
    const handler = withSameOrigin(async () => {
      called = true;
      return Response.json({ ok: true });
    });

    const res = await handler(req("http://evil.com"));
    const body = await res.json();

    expect(res.status).toBe(403);
    expect(body).toEqual({ error: "cross-origin" });
    expect(called).toBe(false);
  });

  it("repassa argumentos extras (ex.: params de rota dinâmica) ao handler", async () => {
    let receivedParams: unknown = null;
    const handler = withSameOrigin(async (_request: NextRequest, params: { treinadorId: string }) => {
      receivedParams = params;
      return Response.json({});
    });

    await handler(req("https://localhost:3000"), { treinadorId: "t-1" });

    expect(receivedParams).toEqual({ treinadorId: "t-1" });
  });
});
