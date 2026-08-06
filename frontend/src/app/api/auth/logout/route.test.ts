import { describe, it, expect, vi } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { POST } from "@/app/api/auth/logout/route";

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
  } as unknown as ReturnType<typeof cookies> extends Promise<infer T> ? T : never);
}

function req(origin: string | undefined, host: string | null = "localhost:3000") {
  const headers = new Headers();
  if (origin !== undefined) headers.set("origin", origin);
  if (host !== null) headers.set("host", host);
  return { headers } as unknown as Parameters<typeof POST>[0];
}

describe("POST /api/auth/logout", () => {
  it("com token → chama backend /conta/logout e limpa cookies", async () => {
    setupCookies({ token: "valid-token", session_guard: "1" });
    let backendCalled = false;
    let receivedAuth: string | null = null;

    server.use(
      http.post("*/conta/logout", ({ request }) => {
        backendCalled = true;
        receivedAuth = request.headers.get("authorization");
        return HttpResponse.json({ ok: true });
      }),
    );

    const res = await POST(req("http://localhost:3000"));
    const body = await res.json();

    expect(backendCalled).toBe(true);
    expect(receivedAuth).toBe("Bearer valid-token");
    expect(body.ok).toBe(true);

    const setCookie = res.headers.get("set-cookie") ?? "";
    for (const name of ["token", "refresh", "session_guard", "tipo_conta"])
      expect(setCookie).toContain(`${name}=;`);
    expect(setCookie).toContain("Path=/");
  });

  it("sem token → nao chama backend e limpa cookies mesmo assim", async () => {
    setupCookies({});
    let backendCalled = false;

    server.use(
      http.post("*/conta/logout", () => {
        backendCalled = true;
        return HttpResponse.json({ ok: true });
      }),
    );

    const res = await POST(req(undefined));
    const body = await res.json();

    expect(backendCalled).toBe(false);
    expect(body.ok).toBe(true);

    const setCookie = res.headers.get("set-cookie");
    expect(setCookie).toContain("token=;");
    expect(setCookie).toContain("session_guard=;");
  });

  it("backend falha → falha silenciosa, cookies ainda limpos", async () => {
    setupCookies({ token: "valid-token" });

    server.use(http.post("*/conta/logout", () => HttpResponse.error()));

    const res = await POST(req("http://localhost:3000"));
    expect(res.status).toBe(200);

    const setCookie = res.headers.get("set-cookie") ?? "";
    for (const name of ["token", "refresh", "session_guard", "tipo_conta"])
      expect(setCookie).toContain(`${name}=;`);
  });

  it("cross-origin → 403, sem limpar cookies, backend nao chamado", async () => {
    setupCookies({ token: "valid-token", session_guard: "1" });
    let backendCalled = false;

    server.use(
      http.post("*/conta/logout", () => {
        backendCalled = true;
        return HttpResponse.json({ ok: true });
      }),
    );

    const res = await POST(req("http://evil.com"));
    const body = await res.json();

    expect(res.status).toBe(403);
    expect(body).toEqual({ error: "cross-origin" });
    expect(backendCalled).toBe(false);
    expect(res.headers.get("set-cookie")).toBeNull();
  });
});
