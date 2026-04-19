import { NextRequest, NextResponse } from "next/server";
import type { TipoConta } from "@/types";

const PUBLIC_PATHS = ["/", "/login", "/cadastro"];

const AREA_BY_TIPO: Record<TipoConta, string> = {
  SystemAdmin: "/admin",
  Treinador: "/treinador",
  Aluno: "/aluno",
};

export function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;
  const token = request.cookies.get("token")?.value;
  const tipoConta = request.cookies.get("tipoConta")?.value as
    | TipoConta
    | undefined;

  const isPublic =
    PUBLIC_PATHS.some((p) => pathname === p) ||
    pathname.startsWith("/cadastro/");

  // Não autenticado tentando acessar área protegida
  if (!token && !isPublic) {
    const url = request.nextUrl.clone();
    url.pathname = "/login";
    return NextResponse.redirect(url);
  }

  // Autenticado tentando acessar rotas públicas → redireciona para área correta
  if (token && tipoConta && isPublic && pathname !== "/") {
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
