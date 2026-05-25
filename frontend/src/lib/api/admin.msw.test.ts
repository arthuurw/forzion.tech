/**
 * Piloto MSW — Fase 3.
 *
 * Demonstra o padrao novo: apiClient REAL (sem vi.mock) + MSW
 * interceptando HTTP. Substitui o padrao antigo (vi.mock("@/lib/api/client"))
 * que sera migrado em massa na Fase 5.
 *
 * Vantagens demonstradas:
 * - Pega bug de URL/params reais (axios serializa, MSW recebe URL final)
 * - Type-safe via tipos OpenAPI gerados (handlers podem usar types.ts)
 * - Override por teste via server.use(...)
 * - Sem leak entre testes (afterEach resetHandlers automatico)
 */
import { describe, expect, it } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { adminApi } from "@/lib/api/admin";
import { buildAluno } from "@/test/factories";

describe("MSW piloto — adminApi.listAlunos via HTTP real", () => {
  it("intercepta GET /admin/alunos e retorna fixture", async () => {
    const alunos = [buildAluno({ nome: "Joao Silva" }), buildAluno({ nome: "Maria Souza" })];

    server.use(
      http.get("*/admin/alunos", () =>
        HttpResponse.json({
          items: alunos,
          total: alunos.length,
          pagina: 1,
          tamanhoPagina: 20,
        }),
      ),
    );

    const res = await adminApi.listAlunos();

    expect(res.data.items).toHaveLength(2);
    expect(res.data.items[0].nome).toBe("Joao Silva");
    expect(res.data.total).toBe(2);
  });

  it("passa params query corretamente no request real", async () => {
    let receivedUrl: URL | null = null;

    server.use(
      http.get("*/admin/alunos", ({ request }) => {
        receivedUrl = new URL(request.url);
        return HttpResponse.json({ items: [], total: 0, pagina: 1, tamanhoPagina: 20 });
      }),
    );

    await adminApi.listAlunos({ nome: "Joao", status: "Ativo", pagina: 2, tamanhoPagina: 10 });

    expect(receivedUrl).not.toBeNull();
    const url = receivedUrl as unknown as URL;
    expect(url.searchParams.get("nome")).toBe("Joao");
    expect(url.searchParams.get("status")).toBe("Ativo");
    expect(url.searchParams.get("pagina")).toBe("2");
    expect(url.searchParams.get("tamanhoPagina")).toBe("10");
  });

  it("propaga erro 500 do backend como rejeicao do axios", async () => {
    server.use(
      http.get("*/admin/alunos", () =>
        HttpResponse.json({ error: "internal" }, { status: 500 }),
      ),
    );

    await expect(adminApi.listAlunos()).rejects.toThrow();
  });
});
