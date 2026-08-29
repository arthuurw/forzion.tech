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

  describe("Allowlist de prefixos (fail-closed)", () => {
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

    // Health nunca esteve na blocklist antiga (só "internal" estava) — o proxy anônimo
    // alcançava /health/ready do backend por omissão.
    it("primeiro segmento 'health' → 404 sem chamar o backend", async () => {
      const req = createMockRequest({ method: "GET" });
      const res = await GET(req, makeCtx(["health", "ready"]));

      expect(res.status).toBe(404);
      expect(fetchSpy).not.toHaveBeenCalled();
    });

    it("prefixo desconhecido, ausente do allowlist → 404 sem chamar o backend", async () => {
      const req = createMockRequest({ method: "GET" });
      const res = await GET(req, makeCtx(["contas", "internal-teste"]));

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

    it("segmento seguinte contendo 'internal'/'health' não bloqueia — só o primeiro segmento gateia", async () => {
      server.use(
        http.get("*/treinador/internal-health-teste", () => HttpResponse.json({ ok: true })),
      );

      const req = createMockRequest({ method: "GET" });
      const res = await GET(req, makeCtx(["treinador", "internal-health-teste"]));

      expect(res.status).toBe(200);
    });

    // Copiado à mão de `grep -rhoE "apiClient\.(get|post|put|patch|delete)..." src/lib/api/*.ts`
    // (não recalculado do próprio ALLOWED_PATH_PREFIXES — senão o teste provaria só que a
    // constante é igual a si mesma, nunca detectando um prefixo real removido por engano).
    const PREFIXOS_REALMENTE_USADOS = [
      "admin",
      "aluno",
      "alunos",
      "auth",
      "conta",
      "notificacoes",
      "suporte",
      "treinador",
      "treinos",
    ];

    it.each(PREFIXOS_REALMENTE_USADOS)(
      "prefixo real '%s' de src/lib/api continua passando para o backend",
      async (prefixo) => {
        server.use(
          http.get(`*/${prefixo}/sonda`, () => HttpResponse.json({ ok: true })),
        );

        const req = createMockRequest({ method: "GET" });
        const res = await GET(req, makeCtx([prefixo, "sonda"]));

        expect(res.status).toBe(200);
      },
    );
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

  describe("Limite de tamanho do corpo", () => {
    const MAX_BODY_BYTES = 10 * 1024 * 1024;

    it("content-length acima do teto → 413 sem ler o corpo nem chamar o backend", async () => {
      setupCookies({});
      const fetchSpy = vi.spyOn(globalThis, "fetch");
      let arrayBufferCalled = false;

      const req = createMockRequest({
        method: "POST",
        headers: { "content-length": String(MAX_BODY_BYTES + 1) },
        body: { a: 1 },
      });
      Object.defineProperty(req, "arrayBuffer", {
        value: async () => {
          arrayBufferCalled = true;
          return new ArrayBuffer(0);
        },
      });

      const res = await POST(req, makeCtx(["admin", "x"]));
      const body = await res.json();

      expect(res.status).toBe(413);
      expect(body).toEqual({ error: "payload_too_large" });
      expect(arrayBufferCalled).toBe(false);
      expect(fetchSpy).not.toHaveBeenCalled();
      fetchSpy.mockRestore();
    });

    it("content-length no teto exato → segue para o backend", async () => {
      setupCookies({});
      server.use(http.post("*/admin/x", () => HttpResponse.json({})));

      const req = createMockRequest({
        method: "POST",
        headers: { "content-length": String(MAX_BODY_BYTES) },
        body: { a: 1 },
      });
      const res = await POST(req, makeCtx(["admin", "x"]));

      expect(res.status).toBe(200);
    });

    it("sem content-length → não é bloqueado por este guard", async () => {
      setupCookies({});
      server.use(http.post("*/admin/x", () => HttpResponse.json({})));

      const req = createMockRequest({ method: "POST", body: { a: 1 } });
      const res = await POST(req, makeCtx(["admin", "x"]));

      expect(res.status).toBe(200);
    });

    it("GET não é afetado pelo guard de corpo (sem corpo a limitar)", async () => {
      setupCookies({});
      server.use(http.get("*/admin/x", () => HttpResponse.json({})));

      const req = createMockRequest({
        method: "GET",
        headers: { "content-length": String(MAX_BODY_BYTES + 1) },
      });
      const res = await GET(req, makeCtx(["admin", "x"]));

      expect(res.status).toBe(200);
    });
  });

  describe("Timeout do backend", () => {
    it("fetch expira (AbortSignal.timeout) → 504 backend_timeout", async () => {
      setupCookies({});
      const fetchSpy = vi
        .spyOn(globalThis, "fetch")
        .mockRejectedValue(new DOMException("The operation was aborted due to timeout", "TimeoutError"));

      const req = createMockRequest({ method: "GET" });
      const res = await GET(req, makeCtx(["admin", "x"]));
      const body = await res.json();

      expect(res.status).toBe(504);
      expect(body).toEqual({ error: "backend_timeout" });
      fetchSpy.mockRestore();
    });

    it("passa um AbortSignal ao fetch do backend", async () => {
      setupCookies({});
      server.use(http.get("*/admin/x", () => HttpResponse.json({})));
      const fetchSpy = vi.spyOn(globalThis, "fetch");

      const req = createMockRequest({ method: "GET" });
      await GET(req, makeCtx(["admin", "x"]));

      const init = fetchSpy.mock.calls[0]?.[1] as RequestInit | undefined;
      expect(init?.signal).toBeInstanceOf(AbortSignal);
      fetchSpy.mockRestore();
    });

    it("erro de rede que não é timeout propaga (não vira 504 silencioso)", async () => {
      setupCookies({});
      const fetchSpy = vi.spyOn(globalThis, "fetch").mockRejectedValue(new TypeError("fetch failed"));

      const req = createMockRequest({ method: "GET" });
      await expect(GET(req, makeCtx(["admin", "x"]))).rejects.toThrow("fetch failed");
      fetchSpy.mockRestore();
    });
  });
});
