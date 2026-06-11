import { NextRequest, NextResponse } from "next/server";
import { cookies } from "next/headers";

const API_BASE = process.env.API_BASE_URL ?? "https://localhost:7220";

// Headers do cliente que são permitidos de serem repassados ao backend.
// Nenhum outro header (Cookie, Authorization, X-Forwarded-For, etc.) é repassado.
const ALLOWED_REQUEST_HEADERS = ["content-type", "accept"];

async function proxy(request: NextRequest, path: string[]): Promise<NextResponse> {
  // A9 — Sanitização de path: rejeitar segmentos que permitiriam path traversal.
  if (path.some((s) => s === ".." || s === ".")) {
    return NextResponse.json({ error: "Invalid path" }, { status: 400 });
  }

  const cookieStore = await cookies();
  const token = cookieStore.get("token")?.value;

  const url = `${API_BASE}/${path.join("/")}${request.nextUrl.search}`;

  // C7 — Apenas headers explicitamente permitidos são repassados; nunca os cookies
  // ou outros headers sensíveis do cliente. O Bearer é injetado pelo proxy.
  const headers = new Headers();
  for (const name of ALLOWED_REQUEST_HEADERS) {
    const value = request.headers.get(name);
    if (value) headers.set(name, value);
  }
  if (token) headers.set("Authorization", `Bearer ${token}`);

  const hasBody = request.method !== "GET" && request.method !== "HEAD";
  const body = hasBody ? await request.arrayBuffer() : undefined;

  const res = await fetch(url, { method: request.method, headers, body });

  const responseHeaders = new Headers();
  const responseContentType = res.headers.get("Content-Type");
  if (responseContentType) responseHeaders.set("Content-Type", responseContentType);

  // 204/304 não admitem body: o construtor de Response lança se receber um (mesmo vazio).
  // Backend devolve 204 p/ recursos ausentes (ex.: config de relatório ainda não criada).
  const nullBodyStatus = res.status === 204 || res.status === 304;
  const responseBody = nullBodyStatus ? null : await res.arrayBuffer();

  return new NextResponse(responseBody, { status: res.status, headers: responseHeaders });
}

type RouteContext = { params: Promise<{ path: string[] }> };

export async function GET(req: NextRequest, ctx: RouteContext) {
  return proxy(req, (await ctx.params).path);
}
export async function POST(req: NextRequest, ctx: RouteContext) {
  return proxy(req, (await ctx.params).path);
}
export async function PUT(req: NextRequest, ctx: RouteContext) {
  return proxy(req, (await ctx.params).path);
}
export async function PATCH(req: NextRequest, ctx: RouteContext) {
  return proxy(req, (await ctx.params).path);
}
export async function DELETE(req: NextRequest, ctx: RouteContext) {
  return proxy(req, (await ctx.params).path);
}
