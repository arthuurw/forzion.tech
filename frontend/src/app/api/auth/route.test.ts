import { describe, it, expect, vi, beforeEach } from "vitest";

// Mock next/server antes de importar o módulo
vi.mock("next/server", async (importActual) => {
  const actual = await importActual<typeof import("next/server")>();
  return {
    ...actual,
    NextResponse: {
      json: (data: unknown, init?: ResponseInit) => {
        const body = JSON.stringify(data);
        const resp = new Response(body, {
          ...init,
          headers: { "content-type": "application/json", ...(init?.headers ?? {}) },
        });
        const cookies = new Map<string, string>();
        (resp as unknown as Record<string, unknown>).cookies = {
          set: (name: string, value: string) => cookies.set(name, value),
          get: (name: string) => cookies.get(name),
          _map: cookies,
        };
        return resp;
      },
    },
  };
});

vi.mock("@/lib/rateLimit", () => ({
  checkRateLimit: vi.fn(() => true),
  getClientIp: vi.fn(() => "127.0.0.1"),
}));

const mockFetch = vi.fn();
vi.stubGlobal("fetch", mockFetch);

describe("POST /api/auth — resposta de login", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    const FUTURE = Math.floor(Date.now() / 1000) + 3600;
    const fakeJwt = `${btoa(JSON.stringify({ alg: "HS256" }))}.${btoa(JSON.stringify({ exp: FUTURE }))}.sig`;
    mockFetch.mockResolvedValue({
      ok: true,
      json: async () => ({
        token: fakeJwt,
        tipoConta: "Treinador",
        contaId: "abc",
        perfilId: "def",
      }),
    });
  });

  it("resposta JSON não contém o token JWT", async () => {
    const { POST } = await import("@/app/api/auth/route");
    const req = {
      json: async () => ({ email: "test@test.com", senha: "123" }),
      headers: { get: () => null },
    } as never;

    const res = await POST(req);
    const body = await res.json();

    expect(body.token).toBeUndefined();
    expect(body.tipoConta).toBe("Treinador");
    expect(body.contaId).toBe("abc");
    expect(body.perfilId).toBe("def");
  });

  it("rate limit excedido → retorna 429", async () => {
    const { checkRateLimit } = await import("@/lib/rateLimit");
    vi.mocked(checkRateLimit).mockReturnValueOnce(false);

    const { POST } = await import("@/app/api/auth/route");
    const req = {
      json: async () => ({ email: "test@test.com", senha: "123" }),
      headers: { get: () => null },
    } as never;

    const res = await POST(req);
    expect(res.status).toBe(429);
  });
});
