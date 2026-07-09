import { apiClient } from "./client";

export type TipoDocumentoFiscal = "Cpf" | "Cnpj";

export interface EnderecoFiscal {
  logradouro: string;
  numero: string;
  complemento?: string | null;
  bairro: string;
  codigoMunicipioIbge: string;
  uf: string;
  cep: string;
}

export interface ConsultaCepResponse {
  logradouro: string;
  complemento: string;
  bairro: string;
  localidade: string;
  uf: string;
  codigoMunicipioIbge: string;
}

export interface DadosFiscaisResponse {
  tipoDocumento: TipoDocumentoFiscal;
  documento: string;
  razaoSocial: string;
  inscricaoMunicipal?: string | null;
  endereco: EnderecoFiscal;
}

export interface DadosFiscaisPayload {
  tipoDocumento: TipoDocumentoFiscal;
  documento: string;
  razaoSocial: string;
  logradouro: string;
  numero: string;
  bairro: string;
  codigoMunicipioIbge: string;
  uf: string;
  cep: string;
  complemento?: string | null;
  inscricaoMunicipal?: string | null;
}

export const nfseApi = {
  getDadosFiscais() {
    return apiClient.get<DadosFiscaisResponse | null>("/treinador/dados-fiscais");
  },
  salvarDadosFiscais(payload: DadosFiscaisPayload) {
    return apiClient.put<DadosFiscaisResponse>("/treinador/dados-fiscais", payload);
  },
  consultarCep(cep: string, signal?: AbortSignal) {
    return apiClient.get<ConsultaCepResponse>(`/treinador/cep/${cep}`, { signal });
  },
};
