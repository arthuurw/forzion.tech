import type { BrowserContext } from "@playwright/test";

const BASE_URL = process.env.E2E_BASE_URL ?? "http://localhost:3000";

export async function seedConsent(
  context: BrowserContext,
  analytics = false,
): Promise<void> {
  await context.addCookies([
    {
      name: "consent",
      value: encodeURIComponent(JSON.stringify({ v: 1, analytics })),
      url: BASE_URL,
    },
  ]);
}
