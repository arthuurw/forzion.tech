import { describe, it, expect, vi } from "vitest";
import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn() })),
  useParams: vi.fn(() => ({})),
}));

const DADOS = {
  tipoDocumento: "Cpf",
  documento: "39053344705",
  razaoSocial: "João Treinador",
  inscricaoMunicipal: null,
  endereco: {
    logradouro: "Rua A",
    numero: "100",
    complemento: null,
    bairro: "Centro",
    codigoMunicipioIbge: "3550308",
    uf: "SP",
    cep: "01001000",
  },
};

async function renderPage() {
  const { default: Page } = await import("@/app/(treinador)/treinador/dados-fiscais/page");
  render(<Page />);
}

describe("DadosFiscaisTreinadorPage", () => {
  it("carrega dados existentes e salva com sucesso", async () => {
    server.use(
      http.get("*/treinador/dados-fiscais", () => HttpResponse.json(DADOS)),
      http.put("*/treinador/dados-fiscais", () => HttpResponse.json(DADOS)),
    );
    await renderPage();

    await waitFor(() => {
      expect(screen.getByLabelText(/Nome \/ Razão social/i)).toHaveValue("João Treinador");
    });

    fireEvent.click(screen.getByRole("button", { name: /salvar dados fiscais/i }));

    await waitFor(() => {
      expect(screen.getByText(/dados fiscais salvos/i)).toBeInTheDocument();
    });
  });

  it("exibe erro retornado pela API", async () => {
    server.use(
      http.get("*/treinador/dados-fiscais", () => HttpResponse.json(DADOS)),
      http.put("*/treinador/dados-fiscais", () =>
        HttpResponse.json({ detail: "Documento inválido." }, { status: 400 }),
      ),
    );
    await renderPage();

    await waitFor(() => {
      expect(screen.getByLabelText(/Nome \/ Razão social/i)).toHaveValue("João Treinador");
    });

    fireEvent.click(screen.getByRole("button", { name: /salvar dados fiscais/i }));

    await waitFor(() => {
      expect(screen.getByText("Documento inválido.")).toBeInTheDocument();
    });
  });

  it("bloqueia submit com validação client-side quando campos vazios", async () => {
    const putSpy = vi.fn();
    server.use(
      http.get("*/treinador/dados-fiscais", () => HttpResponse.json(null)),
      http.put("*/treinador/dados-fiscais", () => {
        putSpy();
        return HttpResponse.json(DADOS);
      }),
    );
    await renderPage();

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /salvar dados fiscais/i })).toBeInTheDocument();
    });

    fireEvent.submit(screen.getByRole("form", { name: /dados fiscais/i }));

    await waitFor(() => {
      expect(screen.getAllByText(/obrigatório/i).length).toBeGreaterThan(0);
    });
    expect(putSpy).not.toHaveBeenCalled();
  });
});
