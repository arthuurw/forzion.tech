import type { Page } from "@playwright/test";

export async function dismissConsent(page: Page): Promise<void> {
  const banner = page.getByRole("dialog", { name: /cookie|consentimento|lgpd/i });
  await banner.waitFor({ state: "visible", timeout: 3_000 }).catch(() => undefined);
  if (!(await banner.isVisible().catch(() => false))) return;
  await page.getByRole("button", { name: /só essenciais/i }).click();
  await banner.waitFor({ state: "hidden", timeout: 3_000 }).catch(() => undefined);
}
