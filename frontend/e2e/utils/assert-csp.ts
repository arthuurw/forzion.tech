import { expect, type Response } from "@playwright/test";

/**
 * Parser + assertions de Content-Security-Policy. Usado em specs de security
 * (Fase 10) pra validar diretivas obrigatorias.
 *
 * Exemplo:
 *   const res = await page.goto("/");
 *   const csp = parseCsp(res);
 *   assertCspDirective(csp, "default-src", "'self'");
 */

export type CspDirectives = Record<string, string[]>;

export function parseCsp(response: Response | null): CspDirectives {
  if (!response) return {};
  const header =
    response.headers()["content-security-policy"] ??
    response.headers()["content-security-policy-report-only"] ??
    "";
  if (!header) return {};

  const result: CspDirectives = {};
  for (const directive of header.split(";")) {
    const trimmed = directive.trim();
    if (!trimmed) continue;
    const [name, ...values] = trimmed.split(/\s+/);
    result[name.toLowerCase()] = values;
  }
  return result;
}

export function assertCspDirective(
  csp: CspDirectives,
  directive: string,
  expectedSource: string,
): void {
  const sources = csp[directive.toLowerCase()];
  expect(sources, `CSP directive "${directive}" ausente`).toBeDefined();
  expect(sources, `CSP "${directive}" nao contem "${expectedSource}"`).toContain(expectedSource);
}

export function assertCspHasDirective(csp: CspDirectives, directive: string): void {
  expect(csp[directive.toLowerCase()], `CSP directive "${directive}" ausente`).toBeDefined();
}
