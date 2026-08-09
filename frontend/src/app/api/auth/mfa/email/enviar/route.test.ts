import { describe, it, expect } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { POST } from "@/app/api/auth/mfa/email/enviar/route";
import { createMockRequest } from "@/test/setup/api";

describe("POST /api/auth/mfa/email/enviar", () => {
  it("emite X-Forwarded-For com o IP resolvido, ignorando o header forjado pelo cliente", async () => {
    let received: string | null = null;
    server.use(
      http.post("*/auth/mfa/email/enviar", ({ request }) => {
        received = request.headers.get("x-forwarded-for");
        return HttpResponse.json({});
      }),
    );

    const req = createMockRequest({
      method: "POST",
      headers: { "x-real-ip": "203.0.113.7", "x-forwarded-for": "198.51.100.9" },
      cookies: { mfa_pending: "pending-tok" },
    });
    await POST(req);

    expect(received).toBe("203.0.113.7");
  });
});
