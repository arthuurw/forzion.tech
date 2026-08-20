import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import {
  GET,
  POST,
  PUT,
  PATCH,
  DELETE,
} from "@/app/api/backend/[...path]/route";
import { createMockRequest } from "@/test/setup/api";

vi.mock("next/headers", () => ({
  cookies: vi.fn(),
}));

import { cookies } from "next/headers";

function setupCookies(values: Record<string, string>) {
  vi.mocked(cookies).mockResolvedValue({
    get: (name: string) => {
      const value = values[name];
      return value !== undefined ? { name, value } : undefined;
    },
  } as never);
}

function makeCtx(path: string[]) {
  return { params: Promise.resolve({ path }) };
}

describe("Backend proxy /api/backend/[...path]", () => {
  describe("Path sanitization", () => {
    it("rejeita path traversal '..' → 400", async () => {
      setupCookies({});
      const req = createMockRequest({ method: "GET" });
      const res = await GET(req, makeCtx(["foo", "..", "secret"]));
      expect(res.status).toBe(400);
      expect((await res.json()).error).toBe("Invalid path");
    });

    it("rejeita segmento '.' isolado → 400", async () => {
      setupCookies({});
      const req = createMockRequest({ method: "GET" });
      const res = await GET(req, makeCtx([".", "foo"]));
      expect(res.status).toBe(400);
    });

    it("path valido segue para backend", async () => {
      setupCookies({});
      server.use(
        http.get("*/admin/alunos", () => HttpResponse.json({ items: [] })),
      );

      const req = createMockRequest({ method: "GET" });
      const res = await GET(req, makeCtx(["admin", "alunos"]));
      expect(res.status).toBe(200);
    });
  });

  describe("Header allowlist + Bearer injection", () => {
    it("injeta Authorization Bearer quando ha token", async () => {
      setupCookies({ token: "abc123" });
      let receivedAuth: string | null = null;

      server.use(
        http.get("*/admin/x", ({ request }) => {
          receivedAuth = request.headers.get("authorization");
          return HttpResponse.json({});
        }),
      );

      const req = createMockRequest({ method: "GET" });
      await GET(req, makeCtx(["admin", "x"]));
      expect(receivedAuth).toBe("Bearer abc123");
    });

    it("sem token → sem header Authorization", async () => {
      setupCookies({});
      let receivedAuth: string | null = null;

      server.use(
        http.get("*/admin/x", ({ request }) => {
          receivedAuth = request.headers.get("authorization");
          return HttpResponse.json({});
        }),
      );

      const req = createMockRequest({ method: "GET" });
      await GET(req, makeCtx(["admin", "x"]));
      expect(receivedAuth).toBeNull();
    });

    it("repassa apenas headers da allowlist (content-type, accept)", async () => {
      setupCookies({});
      let received: Record<string, string | null> = {};

      server.use(
        http.post("*/admin/x", ({ request }) => {
          received = {
            ct: request.headers.get("content-type"),
            acc: request.headers.get("accept"),
            cookie: request.headers.get("cookie"),
            xff: request.headers.get("x-forwarded-for"),
          };
          return HttpResponse.json({});
        }),
      );

      const req = createMockRequest({
        method: "POST",
        headers: {
          "content-type": "application/json",
          accept: "application/json",
          cookie: "should-not-pass",
          "x-forwarded-for": "evil",
        },
        body: { a: 1 },
      });
      await POST(req, makeCtx(["admin", "x"]));

      expect(received.ct).toBe("application/json");
      expect(received.acc).toBe("application/json");
      expect(received.cookie).toBeNull();
      expect(received.xff).toBeNull();
    });

    it("repassa X-Step-Up-Token ao backend quando presente", async () => {
      setupCookies({});
      let received: string | null = null;

      server.use(
        http.post("*/admin/x", ({ request }) => {
          received = request.headers.get("x-step-up-token");
          return HttpResponse.json({});
        }),
      );

      const req = createMockRequest({
        method: "POST",
        headers: { "x-step-up-token": "stepup-jwt" },
        body: { a: 1 },
      });
      await POST(req, makeCtx(["admin", "x"]));

      expect(received).toBe("stepup-jwt");
    });

    it("sem X-Step-Up-Token → backend não recebe o header", async () => {
      setupCookies({});
      let received: string | null = "sentinel";

      server.use(
        http.post("*/admin/x", ({ request }) => {
          received = request.headers.get("x-step-up-token");
          return HttpResponse.json({});
        }),
      );

      const req = createMockRequest({ method: "POST", body: { a: 1 } });
      await POST(req, makeCtx(["admin", "x"]));

      expect(received).toBeNull();
    });
  });

  describe("Bloqueio do prefixo internal", () => {
    let fetchSpy: ReturnType<typeof vi.spyOn>;

    beforeEach(() => {
      setupCookies({});
      fetchSpy = vi.spyOn(globalThis, "fetch");
    });

    afterEach(() => {
      fetchSpy.mockRestore();
    });

    it.each(["internal", "Internal", "INTERNAL", "%69nternal", "%2569nternal"])(
      "primeiro segmento '%s' → 404 sem chamar o backend",
      async (segment) => {
        const req = createMockRequest({ method: "GET" });
        const res = await GET(req, makeCtx([segment, "lgpd", "contas-elegiveis"]));

        expect(res.status).toBe(404);
        expect(fetchSpy).not.toHaveBeenCalled();
      },
    );

    it.each(["internal", "Internal", "INTERNAL", "%69nternal", "%2569nternal"])(
      "prefixo de agentes sob '%s' → 404 sem chamar o backend",
      async (segment) => {
        const req = createMockRequest({ method: "GET" });
        const res = await GET(req, makeCtx([segment, "agents", "v1", "health"]));

        expect(res.status).toBe(404);
        expect(fetchSpy).not.toHaveBeenCalled();
      },
    );

    it.each([
      ["POST", POST],
      ["PUT", PUT],
      ["PATCH", PATCH],
      ["DELETE", DELETE],
    ] as const)("bloqueia internal em %s também", async (method, handler) => {
      const req = createMockRequest({ method, body: { a: 1 } });
      const res = await handler(req, makeCtx(["internal", "billing", "cobrar"]));

      expect(res.status).toBe(404);
      expect(fetchSpy).not.toHaveBeenCalled();
    });

    it("codificação malformada no primeiro segmento → 400 sem chamar o backend", async () => {
      const req = createMockRequest({ method: "GET" });
      const res = await GET(req, makeCtx(["%zz", "foo"]));

      expect(res.status).toBe(400);
      expect((await res.json()).error).toBe("Invalid path");
      expect(fetchSpy).not.toHaveBeenCalled();
    });

    it("'internal' fora do primeiro segmento não é bloqueado", async () => {
      server.use(
        http.get("*/contas/internal-teste", () => HttpResponse.json({ ok: true })),
      );

      const req = createMockRequest({ method: "GET" });
      const res = await GET(req, makeCtx(["contas", "internal-teste"]));

      expect(res.status).toBe(200);
    });

    it("primeiro segmento que só começa com 'internal' não é bloqueado", async () => {
      server.use(
        http.get("*/internal-teste/contas", () => HttpResponse.json({ ok: true })),
      );

      const req = createMockRequest({ method: "GET" });
      const res = await GET(req, makeCtx(["internal-teste", "contas"]));

      expect(res.status).toBe(200);
    });
  });

  describe("Repasse do IP real ao backend", () => {
    it("emite X-Forwarded-For com o IP resolvido de X-Real-IP", async () => {
      setupCookies({});
      let received: string | null = null;

      server.use(
        http.get("*/admin/x", ({ request }) => {
          received = request.headers.get("x-forwarded-for");
          return HttpResponse.json({});
        }),
      );

      const req = createMockRequest({
        method: "GET",
        headers: { "x-real-ip": "203.0.113.7" },
      });
      await GET(req, makeCtx(["admin", "x"]));

      expect(received).toBe("203.0.113.7");
    });

    it("X-Forwarded-For forjado pelo cliente é substituído pelo IP resolvido", async () => {
      setupCookies({});
      let received: string | null = null;

      server.use(
        http.get("*/admin/x", ({ request }) => {
          received = request.headers.get("x-forwarded-for");
          return HttpResponse.json({});
        }),
      );

      const req = createMockRequest({
        method: "GET",
        headers: { "x-real-ip": "203.0.113.7", "x-forwarded-for": "198.51.100.9" },
      });
      await GET(req, makeCtx(["admin", "x"]));

      expect(received).toBe("203.0.113.7");
      expect(received).not.toBe("198.51.100.9");
    });

    it("sem IP resolvível → header ausente e request segue normal", async () => {
      setupCookies({});
      let received: string | null = "sentinel";

      server.use(
        http.get("*/admin/x", ({ request }) => {
          received = request.headers.get("x-forwarded-for");
          return HttpResponse.json({});
        }),
      );

      const req = createMockRequest({ method: "GET" });
      const res = await GET(req, makeCtx(["admin", "x"]));

      expect(received).toBeNull();
      expect(res.status).toBe(200);
    });
  });

  describe("Forwarding metodos HTTP", () => {
    it.each([
      ["GET", GET],
      ["PUT", PUT],
      ["PATCH", PATCH],
      ["DELETE", DELETE],
    ] as const)("propaga %s", async (method, handler) => {
      setupCookies({});
      let received = "";

      server.use(
        http.all("*/admin/x", ({ request }) => {
          received = request.method;
          return HttpResponse.json({});
        }),
      );

      const req = createMockRequest({ method });
      await handler(req, makeCtx(["admin", "x"]));
      expect(received).toBe(method);
    });
  });

  describe("Resposta", () => {
    it("propaga status do backend", async () => {
      setupCookies({});
      server.use(
        http.get("*/admin/x", () =>
          HttpResponse.json({ error: "unauthorized" }, { status: 401 }),
        ),
      );

      const req = createMockRequest({ method: "GET" });
      const res = await GET(req, makeCtx(["admin", "x"]));
      expect(res.status).toBe(401);
    });

    it("propaga Content-Type do backend", async () => {
      setupCookies({});
      server.use(
        http.get("*/admin/file", () =>
          HttpResponse.text("hello", {
            headers: { "Content-Type": "text/plain" },
          }),
        ),
      );

      const req = createMockRequest({ method: "GET" });
      const res = await GET(req, makeCtx(["admin", "file"]));
      expect(res.headers.get("content-type")).toBe("text/plain");
    });

    // Regressão: backend 204 (ex.: config de relatório ausente) virava 500 porque
    // Response não admite body em 204 — construtor lançava.
    it("propaga 204 sem virar 500", async () => {
      setupCookies({});
      server.use(
        http.get("*/admin/health-report/config", () => new HttpResponse(null, { status: 204 })),
      );

      const req = createMockRequest({ method: "GET" });
      const res = await GET(req, makeCtx(["admin", "health-report", "config"]));
      expect(res.status).toBe(204);
    });
  });

  describe("Origin-check (CSRF defense-in-depth)", () => {
    it("POST cross-origin → 403 cross-origin", async () => {
      setupCookies({});
      const req = createMockRequest({
        method: "POST",
        headers: { origin: "http://evil.com" },
        body: { a: 1 },
      });
      const res = await POST(req, makeCtx(["admin", "x"]));
      expect(res.status).toBe(403);
      expect((await res.json()).error).toBe("cross-origin");
    });

    it("POST same-origin → segue para backend", async () => {
      setupCookies({});
      server.use(http.post("*/admin/x", () => HttpResponse.json({})));

      const req = createMockRequest({
        method: "POST",
        headers: { origin: "http://localhost:3000" },
        body: { a: 1 },
      });
      const res = await POST(req, makeCtx(["admin", "x"]));
      expect(res.status).toBe(200);
    });

    it("GET cross-origin → não é checado (passa)", async () => {
      setupCookies({});
      server.use(http.get("*/admin/x", () => HttpResponse.json({})));

      const req = createMockRequest({
        method: "GET",
        headers: { origin: "http://evil.com" },
      });
      const res = await GET(req, makeCtx(["admin", "x"]));
      expect(res.status).toBe(200);
    });
  });
});
