import { describe, it, expect, vi } from "vitest";
import { render, screen, waitFor, fireEvent, within } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn() })),
  useParams: vi.fn(() => ({})),
}));

const NOTA_ERRO = {
  id: "nf-err",
  treinadorId: "11111111-2222-3333-4444-555555555555",
  tipo: "ComissaoMarketplace",
  status: "Erro",
  valor: 10,
  competenciaInicio: null,
  competenciaFim: null,
  numeroNfse: null,
  chaveAcesso: null,
  dataEmissao: null,
  codigoErro: "E500",
  motivoErro: "Falha na SEFIN",
  criadoEm: "2026-06-01T12:00:00Z",
};

async function renderPage() {
  const { default: Page } = await import("@/app/(admin)/admin/notas-fiscais/page");
  render(<Page />);
}

describe("NotasFiscaisAdminPage", () => {
  it("lista as notas e filtra por status", async () => {
    let ultimoStatus: string | null = "inicial";
    server.use(
      http.get("*/admin/notas-fiscais", ({ request }) => {
        ultimoStatus = new URL(request.url).searchParams.get("status");
        return HttpResponse.json({ itens: [NOTA_ERRO], proximoCursor: null });
      }),
    );
    await renderPage();

    await waitFor(() => {
      expect(screen.getByText("Comissão")).toBeInTheDocument();
    });

    const filtro = screen.getByRole("combobox", { name: /status/i });
    fireEvent.mouseDown(filtro);
    const listbox = screen.getByRole("listbox");
    fireEvent.click(within(listbox).getByRole("option", { name: "Erro" }));

    await waitFor(() => {
      expect(ultimoStatus).toBe("Erro");
    });
  });

  it("reprocessa uma nota em erro", async () => {
    const postSpy = vi.fn();
    server.use(
      http.get("*/admin/notas-fiscais", () =>
        HttpResponse.json({ itens: [NOTA_ERRO], proximoCursor: null }),
      ),
      http.post("*/admin/notas-fiscais/nf-err/reprocessar", () => {
        postSpy();
        return new HttpResponse(null, { status: 204 });
      }),
    );
    await renderPage();

    await waitFor(() => {
      expect(screen.getByText("Comissão")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole("button", { name: /reprocessar emissão/i }));

    await waitFor(() => {
      expect(postSpy).toHaveBeenCalled();
      expect(screen.getByText(/reenfileirada/i)).toBeInTheDocument();
    });
  });

  it("exibe erro da API ao reprocessar nota inválida", async () => {
    server.use(
      http.get("*/admin/notas-fiscais", () =>
        HttpResponse.json({ itens: [NOTA_ERRO], proximoCursor: null }),
      ),
      http.post("*/admin/notas-fiscais/nf-err/reprocessar", () =>
        HttpResponse.json({ detail: "Apenas notas fiscais em erro podem ser reprocessadas." }, { status: 422 }),
      ),
    );
    await renderPage();

    await waitFor(() => {
      expect(screen.getByText("Comissão")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole("button", { name: /reprocessar emissão/i }));

    await waitFor(() => {
      expect(screen.getByText(/em erro podem ser reprocessadas/i)).toBeInTheDocument();
    });
  });
});
