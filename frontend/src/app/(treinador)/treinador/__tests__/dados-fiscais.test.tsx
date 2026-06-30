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

  it("preenche endereço e IBGE ao completar 8 dígitos no CEP", async () => {
    server.use(
      http.get("*/treinador/dados-fiscais", () => HttpResponse.json(null)),
      http.get("*/treinador/cep/01001000", () =>
        HttpResponse.json({
          logradouro: "Praça da Sé",
          complemento: "lado ímpar",
          bairro: "Sé",
          localidade: "São Paulo",
          uf: "sp",
          codigoMunicipioIbge: "3550308",
        }),
      ),
    );
    await renderPage();

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /salvar dados fiscais/i })).toBeInTheDocument();
    });

    fireEvent.change(screen.getByLabelText(/^CEP/i), { target: { value: "01001000" } });

    await waitFor(() => {
      expect(screen.getByLabelText(/Logradouro/i)).toHaveValue("Praça da Sé");
    });
    expect(screen.getByLabelText(/Bairro/i)).toHaveValue("Sé");
    expect(screen.getByLabelText(/^UF/i)).toHaveValue("SP");
    expect(screen.getByLabelText(/Código IBGE/i)).toHaveValue("3550308");
    expect(screen.getByLabelText(/Complemento/i)).toHaveValue("lado ímpar");
    expect(screen.getByLabelText(/Número/i)).toHaveValue("");
    expect(screen.getByLabelText(/Logradouro/i)).toBeEnabled();
    expect(screen.getByLabelText(/Código IBGE/i)).toBeEnabled();
  });

  it("não sobrescreve complemento quando o CEP não retorna complemento", async () => {
    server.use(
      http.get("*/treinador/dados-fiscais", () =>
        HttpResponse.json({ ...DADOS, endereco: { ...DADOS.endereco, complemento: "Casa 2" } }),
      ),
      http.get("*/treinador/cep/20040002", () =>
        HttpResponse.json({
          logradouro: "Av. Rio Branco",
          complemento: "",
          bairro: "Centro",
          localidade: "Rio de Janeiro",
          uf: "RJ",
          codigoMunicipioIbge: "3304557",
        }),
      ),
    );
    await renderPage();

    await waitFor(() => {
      expect(screen.getByLabelText(/Complemento/i)).toHaveValue("Casa 2");
    });

    fireEvent.change(screen.getByLabelText(/^CEP/i), { target: { value: "20040002" } });

    await waitFor(() => {
      expect(screen.getByLabelText(/Logradouro/i)).toHaveValue("Av. Rio Branco");
    });
    expect(screen.getByLabelText(/Complemento/i)).toHaveValue("Casa 2");
  });

  it("não consulta CEP no carregamento inicial dos dados salvos", async () => {
    const cepSpy = vi.fn();
    server.use(
      http.get("*/treinador/dados-fiscais", () => HttpResponse.json(DADOS)),
      http.get("*/treinador/cep/*", () => {
        cepSpy();
        return HttpResponse.json({});
      }),
    );
    await renderPage();

    await waitFor(() => {
      expect(screen.getByLabelText(/Nome \/ Razão social/i)).toHaveValue("João Treinador");
    });
    expect(cepSpy).not.toHaveBeenCalled();
  });

  it("exibe aviso não-bloqueante e mantém campos editáveis quando o serviço de CEP falha", async () => {
    server.use(
      http.get("*/treinador/dados-fiscais", () => HttpResponse.json(null)),
      http.get("*/treinador/cep/*", () => HttpResponse.json({}, { status: 502 })),
    );
    await renderPage();

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /salvar dados fiscais/i })).toBeInTheDocument();
    });

    fireEvent.change(screen.getByLabelText(/^CEP/i), { target: { value: "01001000" } });

    await waitFor(() => {
      expect(screen.getByText(/Não foi possível buscar o CEP/i)).toBeInTheDocument();
    });
    expect(screen.getByLabelText(/Logradouro/i)).toBeEnabled();
  });

  it("distingue CEP inexistente (404) do serviço indisponível na mensagem", async () => {
    server.use(
      http.get("*/treinador/dados-fiscais", () => HttpResponse.json(null)),
      http.get("*/treinador/cep/*", () => HttpResponse.json({}, { status: 404 })),
    );
    await renderPage();

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /salvar dados fiscais/i })).toBeInTheDocument();
    });

    fireEvent.change(screen.getByLabelText(/^CEP/i), { target: { value: "99999999" } });

    await waitFor(() => {
      expect(screen.getByText(/CEP não encontrado/i)).toBeInTheDocument();
    });
  });

  it("não apaga logradouro/bairro já preenchidos quando o CEP único retorna campos vazios", async () => {
    server.use(
      http.get("*/treinador/dados-fiscais", () => HttpResponse.json(DADOS)),
      http.get("*/treinador/cep/*", () =>
        HttpResponse.json({
          logradouro: "",
          complemento: "",
          bairro: "",
          localidade: "Brasília",
          uf: "DF",
          codigoMunicipioIbge: "5300108",
        }),
      ),
    );
    await renderPage();

    await waitFor(() => {
      expect(screen.getByLabelText(/Logradouro/i)).toHaveValue("Rua A");
    });

    fireEvent.change(screen.getByLabelText(/^CEP/i), { target: { value: "70040010" } });

    await waitFor(() => {
      expect(screen.getByLabelText(/^UF/i)).toHaveValue("DF");
    });
    expect(screen.getByLabelText(/Logradouro/i)).toHaveValue("Rua A");
    expect(screen.getByLabelText(/Bairro/i)).toHaveValue("Centro");
    expect(screen.getByLabelText(/Código IBGE/i)).toHaveValue("5300108");
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
