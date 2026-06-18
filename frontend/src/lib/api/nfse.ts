import { apiClient } from "./client";

export type TipoDocumentoFiscal = "Cpf" | "Cnpj";

export type TipoNotaFiscal = "AssinaturaSaaS" | "ComissaoMarketplace";

export type NotaFiscalStatus =
  | "Pendente"
  | "Emitida"
  | "Erro"
  | "BloqueadaDadosFiscais"
  | "CancelamentoSolicitado"
  | "Cancelada"
  | "CancelamentoExpirado";

export interface EnderecoFiscal {
  logradouro: string;
  numero: string;
  complemento?: string | null;
  bairro: string;
  codigoMunicipioIbge: string;
  uf: string;
  cep: string;
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

export interface NotaFiscalResumo {
  id: string;
  tipo: TipoNotaFiscal;
  status: NotaFiscalStatus;
  valor: number;
  competenciaInicio?: string | null;
  competenciaFim?: string | null;
  numeroNfse?: string | null;
  dataEmissao?: string | null;
  temDanfse: boolean;
  criadoEm: string;
}

export interface ListarNotasFiscaisResponse {
  itens: NotaFiscalResumo[];
  proximoCursor?: string | null;
}

export interface NotaFiscalAdmin {
  id: string;
  treinadorId: string;
  tipo: TipoNotaFiscal;
  status: NotaFiscalStatus;
  valor: number;
  competenciaInicio?: string | null;
  competenciaFim?: string | null;
  numeroNfse?: string | null;
  chaveAcesso?: string | null;
  dataEmissao?: string | null;
  codigoErro?: string | null;
  motivoErro?: string | null;
  criadoEm: string;
}

export interface ListarNotasFiscaisAdminResponse {
  itens: NotaFiscalAdmin[];
  proximoCursor?: string | null;
}

export const nfseApi = {
  getDadosFiscais() {
    return apiClient.get<DadosFiscaisResponse | null>("/treinador/dados-fiscais");
  },
  salvarDadosFiscais(payload: DadosFiscaisPayload) {
    return apiClient.put<DadosFiscaisResponse>("/treinador/dados-fiscais", payload);
  },
  listNotasTreinador(params?: { aposId?: string; limite?: number }, signal?: AbortSignal) {
    return apiClient.get<ListarNotasFiscaisResponse>("/treinador/notas-fiscais", { params, signal });
  },
  getDanfse(notaFiscalId: string) {
    return apiClient.get<{ danfseRef: string }>(`/treinador/notas-fiscais/${notaFiscalId}/danfse`);
  },
  listNotasAdmin(params?: { status?: NotaFiscalStatus; treinadorId?: string; aposId?: string; limite?: number }, signal?: AbortSignal) {
    return apiClient.get<ListarNotasFiscaisAdminResponse>("/admin/notas-fiscais", { params, signal });
  },
  reprocessarNota(notaFiscalId: string) {
    return apiClient.post(`/admin/notas-fiscais/${notaFiscalId}/reprocessar`);
  },
};

export const NOTA_FISCAL_STATUS_LABEL: Record<NotaFiscalStatus, string> = {
  Pendente: "Pendente",
  Emitida: "Emitida",
  Erro: "Erro",
  BloqueadaDadosFiscais: "Bloqueada (dados fiscais)",
  CancelamentoSolicitado: "Cancelamento solicitado",
  Cancelada: "Cancelada",
  CancelamentoExpirado: "Cancelamento expirado",
};

export const NOTA_FISCAL_STATUS_COLOR: Record<NotaFiscalStatus, "default" | "success" | "error" | "warning" | "info"> = {
  Pendente: "warning",
  Emitida: "success",
  Erro: "error",
  BloqueadaDadosFiscais: "error",
  CancelamentoSolicitado: "info",
  Cancelada: "default",
  CancelamentoExpirado: "error",
};

export const TIPO_NOTA_FISCAL_LABEL: Record<TipoNotaFiscal, string> = {
  AssinaturaSaaS: "Assinatura",
  ComissaoMarketplace: "Comissão",
};
