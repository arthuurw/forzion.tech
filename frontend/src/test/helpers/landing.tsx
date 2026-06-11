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
  const jsx = await LandingPage();
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
