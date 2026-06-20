import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { SignJWT } from "jose";
import { NextResponse, type NextRequest } from "next/server";
import { proxy as middleware } from "@/proxy";

// E2 (specs/tasks/E-testes.md): assinatura JWT verificada com `jose` REAL
// (sem vi.mock("jose")). Prova que o middleware rejeita token assinado com
// secret errado / expirado — se `jwtVerify` virasse no-op, estes testes falham.
// O teste mockado (middleware.test.ts) permanece intacto noutro arquivo.

vi.mock("next/server", () => ({
  NextResponse: {
    next: vi.fn(),
    redirect: vi.fn(),
  },
}));

const SECRET_A = "secret-A-com-no-minimo-32-caracteres-ok";
const SECRET_B = "secret-B-DIFERENTE-com-32+-caracteres!!";

const FUTURE = Math.floor(Date.now() / 1000) + 3600;
const PAST = Math.floor(Date.now() / 1000) - 3600;

async function signToken(
  secret: string,
  payload: Record<string, unknown>,
  exp: number = FUTURE,
): Promise<string> {
  const key = new TextEncoder().encode(secret);
  return new SignJWT(payload)
    .setProtectedHeader({ alg: "HS256" })
    .setExpirationTime(exp)
    .sign(key);
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

function redirectedTo(): string {
  const url = vi.mocked(NextResponse.redirect).mock.calls[0]?.[0] as { pathname: string };
  return url?.pathname;
}

beforeEach(() => {
  vi.clearAllMocks();
  process.env.JWT_SECRET = SECRET_A;
  delete process.env.JWT_ISSUER;
  delete process.env.JWT_AUDIENCE;
});

afterEach(() => {
  delete process.env.JWT_SECRET;
});

describe("middleware — verificação de assinatura JWT real (jose)", () => {
  it("token assinado com o secret correto → pass-through em área protegida", async () => {
    const token = await signToken(SECRET_A, { tipo_conta: "Aluno" });
    await middleware(makeRequest("/aluno/fichas", { token, session_guard: "1" }));
    expect(NextResponse.next).toHaveBeenCalled();
    expect(NextResponse.redirect).not.toHaveBeenCalled();
  });

  it("token assinado com secret ERRADO (verificado com A) → redirect /login", async () => {
    // Assina com B, mas o middleware verifica com A (process.env.JWT_SECRET).
    const token = await signToken(SECRET_B, { tipo_conta: "Aluno" });
    await middleware(makeRequest("/aluno/fichas", { token, session_guard: "1" }));
    expect(NextResponse.redirect).toHaveBeenCalledOnce();
    expect(redirectedTo()).toBe("/login");
  });

  it("token EXPIRADO assinado com o secret correto → redirect /login", async () => {
    const token = await signToken(SECRET_A, { tipo_conta: "Aluno" }, PAST);
    await middleware(makeRequest("/aluno/fichas", { token, session_guard: "1" }));
    expect(NextResponse.redirect).toHaveBeenCalledOnce();
    expect(redirectedTo()).toBe("/login");
  });

  it("token válido (secret A) em /login → redireciona para a área do tipo_conta", async () => {
    const token = await signToken(SECRET_A, { tipo_conta: "Treinador" });
    await middleware(makeRequest("/login", { token, session_guard: "1" }));
    expect(NextResponse.redirect).toHaveBeenCalledOnce();
    expect(redirectedTo()).toBe("/treinador");
  });
});
