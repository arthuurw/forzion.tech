import type { BrowserContext, CDPSession, Page } from "@playwright/test";

/**
 * Helpers de simulacao de rede via Chrome DevTools Protocol.
 *
 * APIs CDP so funcionam em chromium-* projects. Em firefox/webkit, helpers
 * retornam no-op com warning — testes que dependem de network throttling devem
 * usar `test.skip(browserName !== "chromium")` ou rodar so no project chromium.
 */

export interface NetworkConditions {
  offline: boolean;
  downloadThroughput: number; // bytes/s
  uploadThroughput: number; // bytes/s
  latency: number; // ms
}

// Presets canonicos. Numbers ~Chrome DevTools defaults.
export const NETWORK_PRESETS = {
  slow3G: {
    offline: false,
    downloadThroughput: (500 * 1024) / 8, // 500 Kbps
    uploadThroughput: (500 * 1024) / 8,
    latency: 400,
  },
  fast3G: {
    offline: false,
    downloadThroughput: (1.6 * 1024 * 1024) / 8, // 1.6 Mbps
    uploadThroughput: (750 * 1024) / 8,
    latency: 150,
  },
  offline: {
    offline: true,
    downloadThroughput: 0,
    uploadThroughput: 0,
    latency: 0,
  },
  online: {
    offline: false,
    downloadThroughput: -1,
    uploadThroughput: -1,
    latency: 0,
  },
} satisfies Record<string, NetworkConditions>;

async function getCdpSession(
  context: BrowserContext,
  page: Page,
): Promise<CDPSession | null> {
  try {
    return await context.newCDPSession(page);
  } catch {
    return null;
  }
}

export async function applyNetwork(
  context: BrowserContext,
  page: Page,
  conditions: NetworkConditions,
): Promise<void> {
  const cdp = await getCdpSession(context, page);
  if (!cdp) {
     
    console.warn("[network fixture] CDP indisponivel — network throttling ignorado");
    return;
  }
  await cdp.send("Network.enable");
  await cdp.send("Network.emulateNetworkConditions", conditions);
}

export async function goOffline(context: BrowserContext, page: Page): Promise<void> {
  await context.setOffline(true);
  await applyNetwork(context, page, NETWORK_PRESETS.offline);
}

export async function goOnline(context: BrowserContext, page: Page): Promise<void> {
  await context.setOffline(false);
  await applyNetwork(context, page, NETWORK_PRESETS.online);
}

/**
 * Simula falha intermitente: as primeiras N respostas pra um URL pattern caem
 * com status 503; depois disso, passa adiante. Util pra validar retry/backoff
 * em apiClient.
 */
export async function flakyRoute(
  page: Page,
  urlPattern: string | RegExp,
  failCount: number,
  failStatus = 503,
): Promise<{ attempts: () => number; restore: () => Promise<void> }> {
  let attempts = 0;
  await page.route(urlPattern, async (route) => {
    attempts += 1;
    if (attempts <= failCount) {
      await route.fulfill({
        status: failStatus,
        contentType: "application/json",
        body: JSON.stringify({ error: "simulated failure" }),
      });
      return;
    }
    await route.continue();
  });
  return {
    attempts: () => attempts,
    restore: () => page.unroute(urlPattern),
  };
}
