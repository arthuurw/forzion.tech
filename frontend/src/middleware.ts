import { NextRequest, NextResponse } from "next/server";
import type { TipoConta } from "@/types";

const PUBLIC_PATHS = ["/", "/login", "/cadastro"];

const AREA_BY_TIPO: Record<TipoConta, string> = {
  SystemAdmin: "/admin",
  Treinador: "/treinador",
  Aluno: "/aluno",
};

function extractTipoConta(token: string): TipoConta | null {
  try {
    const parts = token.split(".");
    if (parts.length !== 3) return null;
    const payload = parts[1].replace(/-/g, "+").replace(/_/g, "/");
    const json = JSON.parse(atob(payload)) as { tipo_conta?: string; exp?: number };
    if (json.exp && json.exp * 1000 < Date.now()) return null;
    return (json.tipo_conta as TipoConta) ?? null;
  } catch {
    return null;
  }
}

export default function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;
  const token = request.cookies.get("token")?.value;
  const tipoConta = token ? extractTipoConta(token) : null;

  const isPublic =
    PUBLIC_PATHS.some((p) => pathname === p) ||
    pathname.startsWith("/cadastro/");

  // Cadastro é sempre acessível — autenticado ou não
  if (pathname.startsWith("/cadastro/")) return NextResponse.next();

  // Não autenticado tentando acessar área protegida
  if (!token && !isPublic) {
    const url = request.nextUrl.clone();
    url.pathname = "/login";
    return NextResponse.redirect(url);
  }

  // Autenticado no login → redireciona para área correta
  if (token && tipoConta && pathname === "/login") {
    const url = request.nextUrl.clone();
    url.pathname = AREA_BY_TIPO[tipoConta] ?? "/";
    return NextResponse.redirect(url);
  }

  // Autenticado acessando área errada (ex: Aluno tentando /admin)
  if (token && tipoConta) {
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
  matcher: ["/((?!_next/static|_next/image|favicon.ico|api/).*)"],
};
