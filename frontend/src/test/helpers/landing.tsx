import { vi, beforeEach, afterEach } from "vitest";
import { render } from "@testing-library/react";
import { buildPlano } from "@/test/factories/plano";

const _originalFetch = global.fetch;

export async function renderLanding(
  planos: ReturnType<typeof buildPlano>[],
): Promise<void> {
  global.fetch = vi.fn().mockResolvedValue({
    ok: true,
    json: async () => planos,
  });
  const { default: LandingPage } = await import("@/app/page");
  render(LandingPage());
}

export async function renderPlanos(
  planos: ReturnType<typeof buildPlano>[],
): Promise<void> {
  global.fetch = vi.fn().mockResolvedValue({
    ok: true,
    json: async () => planos,
  });
  const { default: PlanosSlab } = await import("@/app/_landing/PlanosSlab");
  const jsx = await PlanosSlab();
  render(jsx);
}

export function setupLandingTest(): void {
  beforeEach(() => {
    vi.resetModules();
  });
  afterEach(() => {
    global.fetch = _originalFetch;
  });
}
