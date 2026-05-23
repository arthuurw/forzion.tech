import { setupServer } from "msw/node";
import { handlers } from "./handlers";

/**
 * Servidor MSW para tests Node (vitest project "integration").
 *
 * Lifecycle controlado em src/test/setup/integration.ts:
 *  - beforeAll: server.listen({ onUnhandledRequest: "error" })
 *  - afterEach: server.resetHandlers()
 *  - afterAll:  server.close()
 *
 * Override por teste:
 *   server.use(http.get("/foo", () => HttpResponse.json({ x: 1 })));
 */
export const server = setupServer(...handlers);
