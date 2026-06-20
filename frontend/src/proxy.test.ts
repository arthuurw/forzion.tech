import { beforeEach, describe, expect, it, vi } from "vitest";
import { NextResponse, type NextRequest } from "next/server";
import { proxy as middleware } from "@/proxy";

vi.mock("next/server", () => ({
  NextResponse: {
    next: vi.fn(),
    redirect: vi.fn(),
  },
}));

// jose mock.
// The middleware uses jwtVerify from "jose". In unit tests we mock it so that:
//  - tokens whose payload contains a recognised tipo_conta → resolve successfully
//  - tokens flagged as INVALID_SIG → reject (simulate signature mismatch)
//  - expired tokens (exp in past) still reject via jose (lifetime validation)

const FUTURE = Math.floor(Date.now() / 1000) + 3600;

// Tokens that should be treated as having an invalid signature.
const INVALID_SIG_SENTINEL = "__INVALID_SIG__";

vi.mock("jose", () => ({
  jwtVerify: vi.fn(async (token: string) => {
    if (token === INVALID_SIG_SENTINEL) {
      throw new Error("JWTSignatureVerificationFailed");
    }
    // Decode payload from the fake JWT used in tests.
    const parts = token.split(".");
    if (parts.length !== 3) throw new Error("invalid token");
    const payload = JSON.parse(atob(parts[1].replace(/-/g, "+").replace(/_/g, "/")));
    // Simulate lifetime check: reject if expired.
    if (payload.exp && payload.exp * 1000 < Date.now()) {
      throw new Error("JWTExpired");
    }
    return { payload };
  }),
}));

function makeJwt(payload: Record<string, unknown>): string {
  const header = btoa(JSON.stringify({ alg: "HS256", typ: "JWT" }));
  const body = btoa(JSON.stringify(payload));
  return `${header}.${body}.fakesig`;
}

function makeRequest(pathname: string, cookies: Record<string, string> = {}): NextRequest {
  return {
    nextUrl: {
      pathname,
      clone: () => ({ pathname } as { pathname: string }),
    },
    cookies: {
      get: (name: string) => {
        const val = cookies[name];
        return val !== undefined ? { name, value: val } : undefined;
      },
    },
  } as unknown as NextRequest;
}

function validCookies(tipoConta: string): Record<string, string> {
  return {
    token: makeJwt({ tipo_conta: tipoConta, exp: FUTURE }),
    session_guard: "1",
  };
}

function redirectedTo(): string {
  const url = vi.mocked(NextResponse.redirect).mock.calls[0]?.[0] as { pathname: string };
  return url?.pathname;
}

beforeEach(() => vi.clearAllMocks());

describe("middleware — rotas públicas", () => {
  it("/ sem auth → pass-through", async () => {
    await middleware(makeRequest("/"));
    expect(NextResponse.next).toHaveBeenCalled();
    expect(NextResponse.redirect).not.toHaveBeenCalled();
  });

  it("/login sem auth → pass-through", async () => {
    await middleware(makeRequest("/login"));
    expect(NextResponse.next).toHaveBeenCalled();
    expect(NextResponse.redirect).not.toHaveBeenCalled();
  });

  it("/resend-verification sem auth → pass-through", async () => {
    await middleware(makeRequest("/resend-verification"));
    expect(NextResponse.next).toHaveBeenCalled();
    expect(NextResponse.redirect).not.toHaveBeenCalled();
  });

  it("/cadastro/aluno sem auth → pass-through", async () => {
    await middleware(makeRequest("/cadastro/aluno"));
    expect(NextResponse.next).toHaveBeenCalled();
    expect(NextResponse.redirect).not.toHaveBeenCalled();
  });

  it("/cadastro/treinador mesmo autenticado → pass-through", async () => {
    await middleware(makeRequest("/cadastro/treinador", validCookies("Aluno")));
    expect(NextResponse.next).toHaveBeenCalled();
    expect(NextResponse.redirect).not.toHaveBeenCalled();
  });
});

describe("middleware — sem auth em área protegida", () => {
  it.each([
    ["/admin"],
    ["/treinador/alunos"],
    ["/aluno/fichas"],
    ["/perfil"],
  ])("%s → redirect /login", async (path) => {
    await middleware(makeRequest(path));
    expect(NextResponse.redirect).toHaveBeenCalledOnce();
    expect(redirectedTo()).toBe("/login");
  });

  it("token sem session_guard → trata como não autenticado → redirect /login", async () => {
    const token = makeJwt({ tipo_conta: "Aluno", exp: FUTURE });
    await middleware(makeRequest("/aluno/fichas", { token })); // session_guard ausente
    expect(redirectedTo()).toBe("/login");
  });
});

// G-SEC-3: assinatura JWT inválida
describe("middleware — assinatura JWT inválida", () => {
  it("token com assinatura inválida em área protegida → redirect /login", async () => {
    await middleware(makeRequest("/aluno/fichas", {
      token: INVALID_SIG_SENTINEL,
      session_guard: "1",
    }));
    expect(NextResponse.redirect).toHaveBeenCalledOnce();
    expect(redirectedTo()).toBe("/login");
  });

  it("token com assinatura inválida em /login → pass-through (não redireciona para dashboard)", async () => {
    await middleware(makeRequest("/login", {
      token: INVALID_SIG_SENTINEL,
      session_guard: "1",
    }));
    // Treated as unauthenticated → /login is public → next()
    expect(NextResponse.next).toHaveBeenCalled();
    expect(NextResponse.redirect).not.toHaveBeenCalled();
  });

  // Defense-in-depth: assinatura válida mas claim tipo_conta forjado → não-autenticado.
  it("token com tipo_conta forjado (assinatura válida) → redirect /login", async () => {
    await middleware(makeRequest("/aluno/fichas", {
      token: makeJwt({ tipo_conta: "Hacker", exp: FUTURE }),
      session_guard: "1",
    }));
    expect(NextResponse.redirect).toHaveBeenCalledOnce();
    expect(redirectedTo()).toBe("/login");
  });

  it("token expirado em área protegida → redirect /login", async () => {
    const PAST = Math.floor(Date.now() / 1000) - 3600;
    const expiredToken = makeJwt({ tipo_conta: "Aluno", exp: PAST });
    await middleware(makeRequest("/aluno/fichas", {
      token: expiredToken,
      session_guard: "1",
    }));
    expect(NextResponse.redirect).toHaveBeenCalledOnce();
    expect(redirectedTo()).toBe("/login");
  });
});

describe("middleware — autenticado em /login", () => {
  it.each([
    ["SystemAdmin", "/admin"],
    ["Treinador",   "/treinador"],
    ["Aluno",       "/aluno"],
  ] as const)("%s → redirect %s", async (tipoConta, expected) => {
    await middleware(makeRequest("/login", validCookies(tipoConta)));
    expect(NextResponse.redirect).toHaveBeenCalledOnce();
    expect(redirectedTo()).toBe(expected);
  });
});

describe("middleware — papel errado na rota", () => {
  it("Aluno em /admin → redirect /aluno", async () => {
    await middleware(makeRequest("/admin", validCookies("Aluno")));
    expect(redirectedTo()).toBe("/aluno");
  });

  it("Treinador em /aluno/fichas → redirect /treinador", async () => {
    await middleware(makeRequest("/aluno/fichas", validCookies("Treinador")));
    expect(redirectedTo()).toBe("/treinador");
  });

  it("SystemAdmin em /treinador/alunos → redirect /admin", async () => {
    await middleware(makeRequest("/treinador/alunos", validCookies("SystemAdmin")));
    expect(redirectedTo()).toBe("/admin");
  });
});

describe("middleware — leniência com refresh presente (access expirado)", () => {
  it("sem access mas refresh + hint na área própria → pass-through (refresh silencioso)", async () => {
    await middleware(makeRequest("/aluno/fichas", { refresh: "raw", tipo_conta: "Aluno" }));
    expect(NextResponse.next).toHaveBeenCalled();
    expect(NextResponse.redirect).not.toHaveBeenCalled();
  });

  it("sem access mas refresh sem hint em área protegida → pass-through (cliente renova)", async () => {
    await middleware(makeRequest("/aluno/fichas", { refresh: "raw" }));
    expect(NextResponse.next).toHaveBeenCalled();
    expect(NextResponse.redirect).not.toHaveBeenCalled();
  });

  it("refresh + hint Aluno em /admin → redirect /aluno (roteia pelo hint)", async () => {
    await middleware(makeRequest("/admin", { refresh: "raw", tipo_conta: "Aluno" }));
    expect(redirectedTo()).toBe("/aluno");
  });

  it("refresh + hint Treinador em /login → redirect /treinador", async () => {
    await middleware(makeRequest("/login", { refresh: "raw", tipo_conta: "Treinador" }));
    expect(redirectedTo()).toBe("/treinador");
  });

  it("hint forjado (tipo_conta inválido) + refresh em área protegida → pass-through, sem rotear", async () => {
    await middleware(makeRequest("/aluno/fichas", { refresh: "raw", tipo_conta: "Hacker" }));
    expect(NextResponse.next).toHaveBeenCalled();
    expect(NextResponse.redirect).not.toHaveBeenCalled();
  });

  it("sem access e SEM refresh em área protegida → redirect /login", async () => {
    await middleware(makeRequest("/aluno/fichas", {}));
    expect(redirectedTo()).toBe("/login");
  });
});

describe("middleware — autenticado na área correta", () => {
  it.each([
    ["Aluno",       "/aluno/fichas"],
    ["Treinador",   "/treinador/alunos"],
    ["SystemAdmin", "/admin"],
  ] as const)("%s em %s → pass-through", async (tipoConta, path) => {
    await middleware(makeRequest(path, validCookies(tipoConta)));
    expect(NextResponse.next).toHaveBeenCalled();
    expect(NextResponse.redirect).not.toHaveBeenCalled();
  });

  it("autenticado em / → pass-through", async () => {
    await middleware(makeRequest("/", validCookies("Aluno")));
    expect(NextResponse.next).toHaveBeenCalled();
    expect(NextResponse.redirect).not.toHaveBeenCalled();
  });
});
