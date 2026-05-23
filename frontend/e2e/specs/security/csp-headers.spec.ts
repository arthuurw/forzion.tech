import { test, expect } from "../../fixtures/test-base";
import {
  parseCsp,
  assertCspDirective,
  assertCspHasDirective,
} from "../../utils/assert-csp";

/**
 * Security 1/4 — Content-Security-Policy headers presentes em paginas-chave.
 *
 * CSP definido em next.config.ts (buildCsp). Valida:
 * - default-src 'self'
 * - script-src 'self' 'unsafe-inline' https://js.stripe.com
 * - frame-ancestors 'none'
 * - connect-src 'self' https://api.stripe.com
 * - X-Frame-Options DENY
 * - Strict-Transport-Security max-age
 *
 * Nao requer auth.
 */

test.describe("security: CSP + security headers", () => {
  test("landing tem CSP com diretivas essenciais", async ({ page }) => {
    const response = await page.goto("/");
    expect(response).not.toBeNull();
    const csp = parseCsp(response);
    assertCspDirective(csp, "default-src", "'self'");
    assertCspDirective(csp, "script-src", "'self'");
    assertCspDirective(csp, "script-src", "https://js.stripe.com");
    assertCspDirective(csp, "frame-ancestors", "'none'");
    assertCspDirective(csp, "connect-src", "'self'");
    assertCspDirective(csp, "connect-src", "https://api.stripe.com");
    assertCspHasDirective(csp, "base-uri");
    assertCspHasDirective(csp, "form-action");
  });

  test("login tem mesmas diretivas CSP", async ({ page }) => {
    const response = await page.goto("/login");
    expect(response).not.toBeNull();
    const csp = parseCsp(response);
    assertCspDirective(csp, "default-src", "'self'");
    assertCspDirective(csp, "frame-ancestors", "'none'");
  });

  test("headers de seguranca complementares", async ({ page }) => {
    const response = await page.goto("/");
    expect(response).not.toBeNull();
    const headers = response!.headers();
    expect(headers["x-frame-options"], "X-Frame-Options").toBe("DENY");
    expect(headers["x-content-type-options"], "X-Content-Type-Options").toBe("nosniff");
    expect(headers["referrer-policy"], "Referrer-Policy").toBe(
      "strict-origin-when-cross-origin",
    );
    expect(headers["strict-transport-security"], "HSTS").toContain("max-age=");
    expect(headers["permissions-policy"], "Permissions-Policy").toContain("camera=()");
  });
});
