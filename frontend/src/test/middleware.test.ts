import { beforeEach, describe, expect, it, vi } from "vitest";
import { NextResponse, type NextRequest } from "next/server";
import middleware from "@/middleware";

vi.mock("next/server", () => ({
  NextResponse: {
    next: vi.fn(),
    redirect: vi.fn(),
  },
}));

const FUTURE = Math.floor(Date.now() / 1000) + 3600;

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

// ─── Rotas públicas ──────────────────────────────────────────────────────────

describe("middleware — rotas públicas", () => {
  it("/ sem auth → pass-through", () => {
    middleware(makeRequest("/"));
    expect(NextResponse.next).toHaveBeenCalled();
    expect(NextResponse.redirect).not.toHaveBeenCalled();
  });

  it("/login sem auth → pass-through", () => {
    middleware(makeRequest("/login"));
    expect(NextResponse.next).toHaveBeenCalled();
    expect(NextResponse.redirect).not.toHaveBeenCalled();
  });

  it("/cadastro/aluno sem auth → pass-through", () => {
    middleware(makeRequest("/cadastro/aluno"));
    expect(NextResponse.next).toHaveBeenCalled();
    expect(NextResponse.redirect).not.toHaveBeenCalled();
  });

  it("/cadastro/treinador mesmo autenticado → pass-through", () => {
    middleware(makeRequest("/cadastro/treinador", validCookies("Aluno")));
    expect(NextResponse.next).toHaveBeenCalled();
    expect(NextResponse.redirect).not.toHaveBeenCalled();
  });
});

// ─── Não autenticado em área protegida ──────────────────────────────────────

describe("middleware — sem auth em área protegida", () => {
  it.each([
    ["/admin"],
    ["/treinador/alunos"],
    ["/aluno/fichas"],
    ["/perfil"],
  ])("%s → redirect /login", (path) => {
    middleware(makeRequest(path));
    expect(NextResponse.redirect).toHaveBeenCalledOnce();
    expect(redirectedTo()).toBe("/login");
  });

  it("token sem session_guard → trata como não autenticado → redirect /login", () => {
    const token = makeJwt({ tipo_conta: "Aluno", exp: FUTURE });
    middleware(makeRequest("/aluno/fichas", { token })); // session_guard ausente
    expect(redirectedTo()).toBe("/login");
  });
});

// ─── Autenticado em /login ───────────────────────────────────────────────────

describe("middleware — autenticado em /login", () => {
  it.each([
    ["SystemAdmin", "/admin"],
    ["Treinador",   "/treinador"],
    ["Aluno",       "/aluno"],
  ] as const)("%s → redirect %s", (tipoConta, expected) => {
    middleware(makeRequest("/login", validCookies(tipoConta)));
    expect(NextResponse.redirect).toHaveBeenCalledOnce();
    expect(redirectedTo()).toBe(expected);
  });
});

// ─── Papel errado na rota ────────────────────────────────────────────────────

describe("middleware — papel errado na rota", () => {
  it("Aluno em /admin → redirect /aluno", () => {
    middleware(makeRequest("/admin", validCookies("Aluno")));
    expect(redirectedTo()).toBe("/aluno");
  });

  it("Treinador em /aluno/fichas → redirect /treinador", () => {
    middleware(makeRequest("/aluno/fichas", validCookies("Treinador")));
    expect(redirectedTo()).toBe("/treinador");
  });

  it("SystemAdmin em /treinador/alunos → redirect /admin", () => {
    middleware(makeRequest("/treinador/alunos", validCookies("SystemAdmin")));
    expect(redirectedTo()).toBe("/admin");
  });
});

// ─── Autenticado na área correta ─────────────────────────────────────────────

describe("middleware — autenticado na área correta", () => {
  it.each([
    ["Aluno",       "/aluno/fichas"],
    ["Treinador",   "/treinador/alunos"],
    ["SystemAdmin", "/admin"],
  ] as const)("%s em %s → pass-through", (tipoConta, path) => {
    middleware(makeRequest(path, validCookies(tipoConta)));
    expect(NextResponse.next).toHaveBeenCalled();
    expect(NextResponse.redirect).not.toHaveBeenCalled();
  });

  it("autenticado em / → pass-through", () => {
    middleware(makeRequest("/", validCookies("Aluno")));
    expect(NextResponse.next).toHaveBeenCalled();
    expect(NextResponse.redirect).not.toHaveBeenCalled();
  });
});
