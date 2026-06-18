import { describe, it, expect, vi } from "vitest";
import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn() })),
  useParams: vi.fn(() => ({})),
}));

const NOTA = {
  id: "nf-1",
  tipo: "AssinaturaSaaS",
  status: "Emitida",
  valor: 99.9,
  competenciaInicio: null,
  competenciaFim: null,
  numeroNfse: "123",
  dataEmissao: "2026-06-01T12:00:00Z",
  temDanfse: true,
  criadoEm: "2026-06-01T12:00:00Z",
};

async function renderPage() {
  const { default: Page } = await import("@/app/(treinador)/treinador/notas-fiscais/page");
  render(<Page />);
}

describe("NotasFiscaisTreinadorPage", () => {
  it("renderiza a lista de notas", async () => {
    server.use(
      http.get("*/treinador/notas-fiscais", () =>
        HttpResponse.json({ itens: [NOTA], proximoCursor: null }),
      ),
    );
    await renderPage();

    await waitFor(() => {
      expect(screen.getByText("Assinatura")).toBeInTheDocument();
    });
    expect(screen.getByText("Emitida")).toBeInTheDocument();
  });

  it("baixa a DANFSe ao clicar no botão", async () => {
    const openSpy = vi.spyOn(window, "open").mockReturnValue(null);
    server.use(
      http.get("*/treinador/notas-fiscais", () =>
        HttpResponse.json({ itens: [NOTA], proximoCursor: null }),
      ),
      http.get("*/treinador/notas-fiscais/nf-1/danfse", () =>
        HttpResponse.json({ danfseRef: "https://nfse.example/danfse/nf-1.pdf" }),
      ),
    );
    await renderPage();

    await waitFor(() => {
      expect(screen.getByText("Assinatura")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole("button", { name: /baixar danfse/i }));

    await waitFor(() => {
      expect(openSpy).toHaveBeenCalledWith(
        "https://nfse.example/danfse/nf-1.pdf",
        "_blank",
        "noopener",
      );
    });
    openSpy.mockRestore();
  });

  it("exibe estado vazio quando não há notas", async () => {
    server.use(
      http.get("*/treinador/notas-fiscais", () =>
        HttpResponse.json({ itens: [], proximoCursor: null }),
      ),
    );
    await renderPage();

    await waitFor(() => {
      expect(screen.getByText(/nenhuma nota fiscal emitida/i)).toBeInTheDocument();
    });
  });
});
