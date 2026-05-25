import { setupWorker } from "msw/browser";
import { handlers } from "./handlers";

/**
 * Worker MSW para Storybook e modo dev opcional (browser).
 *
 * Inicializar pela aplicacao apenas quando NODE_ENV !== "production":
 *   const { worker } = await import("@/test/msw/browser");
 *   await worker.start({ onUnhandledRequest: "bypass" });
 *
 * Arquivo de service worker registrado: public/mockServiceWorker.js
 * (gerar via "npx msw init public/" quando habilitar uso browser).
 */
export const worker = setupWorker(...handlers);
