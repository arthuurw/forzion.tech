import { NextRequest, NextResponse } from "next/server";
import { jwtVerify } from "jose";
import type { TipoConta } from "@/types";
import { parseTipoConta } from "@/lib/auth/jwt";

const PUBLIC_PATHS = ["/", "/login", "/cadastro", "/forgot-password", "/reset-password", "/verify-email", "/resend-verification"];

const AREA_BY_TIPO: Record<TipoConta, string> = {
  SystemAdmin: "/admin",
  Treinador: "/treinador",
  Aluno: "/aluno",
};

async function verifyTipoConta(token: string): Promise<TipoConta | null> {
  try {
    const secret = new TextEncoder().encode(process.env.JWT_SECRET ?? "");
    const { payload } = await jwtVerify(token, secret, {
      issuer: process.env.JWT_ISSUER,
      audience: process.env.JWT_AUDIENCE,
    });
    return parseTipoConta(payload["tipo_conta"]);
  } catch {
    return null;
  }
}

export default async function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;
  const token = request.cookies.get("token")?.value;
  const sessionGuard = request.cookies.get("session_guard")?.value;
  const tipoConta = token && sessionGuard ? await verifyTipoConta(token) : null;

  const isPublic =
    PUBLIC_PATHS.some((p) => pathname === p) ||
    pathname.startsWith("/cadastro/");

  // Cadastro é sempre acessível — autenticado ou não
  if (pathname.startsWith("/cadastro/")) return NextResponse.next();

  // Não autenticado (ou token expirado) tentando acessar área protegida
  if (!tipoConta && !isPublic) {
    const url = request.nextUrl.clone();
    url.pathname = "/login";
    return NextResponse.redirect(url);
  }

  // Autenticado em "/login" → redireciona para área correta (evita ver form de login).
  // "/" é sempre acessível — homepage mostra CTA para o dashboard client-side.
  if (tipoConta && pathname === "/login") {
    const url = request.nextUrl.clone();
    url.pathname = AREA_BY_TIPO[tipoConta];
    return NextResponse.redirect(url);
  }

  // Autenticado acessando área errada (ex: Aluno tentando /admin)
  if (tipoConta) {
    const allowedPrefix = AREA_BY_TIPO[tipoConta];
    const isProtected =
      pathname.startsWith("/admin") ||
      pathname.startsWith("/treinador") ||
      pathname.startsWith("/aluno");

    if (isProtected && !pathname.startsWith(allowedPrefix)) {
      const url = request.nextUrl.clone();
      url.pathname = allowedPrefix;
      return NextResponse.redirect(url);
    }
  }

  return NextResponse.next();
}

export const config = {
  // `.*\..*` exclui assets estáticos do /public (ex: /screenshots/*.webp, robots.txt):
  // sem isso o guard de auth redireciona o asset pra /login (307) e o next/image quebra (400).
  matcher: ["/((?!_next/static|_next/image|favicon.ico|api/|.*\\..*).*)"],
};
