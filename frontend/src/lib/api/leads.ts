import { apiClient } from "./client";
import type {
  LeadStatus,
  LeadSource,
  TipoContatoLead,
  MotivoDescarteLead,
  ListarLeadsResponse,
  LeadDetalheResponse,
  ObterMetricasLeadsResponse,
  EnviarConviteLeadResponse,
} from "@/types";

export interface ListarLeadsParams {
  pagina?: number;
  tamanhoPagina?: number;
  status?: LeadStatus;
  origem?: LeadSource;
  inicio?: string;
  fim?: string;
  termo?: string;
}

export interface CriarLeadManualPayload {
  nome: string;
  contatoTipo: TipoContatoLead;
  contatoValor: string;
  interesse?: string | null;
  consentimentoFinalidade: string;
}

export interface CriarLeadManualResponse {
  leadId: string;
}

export interface AtualizarStatusLeadPayload {
  novoStatus: Extract<LeadStatus, "EmContato" | "Descartado">;
  motivo?: MotivoDescarteLead | null;
  observacao?: string | null;
}

export interface AtualizarStatusLeadResponse {
  leadId: string;
  status: LeadStatus;
}

export interface RegistrarInteracaoLeadResponse {
  leadId: string;
}

export const leadsApi = {
  listar(params: ListarLeadsParams) {
    return apiClient.get<ListarLeadsResponse>("/treinador/leads", { params });
  },
  obter(id: string) {
    return apiClient.get<LeadDetalheResponse>(`/treinador/leads/${id}`);
  },
  criarManual(payload: CriarLeadManualPayload) {
    return apiClient.post<CriarLeadManualResponse>("/treinador/leads", payload);
  },
  atualizarStatus(id: string, payload: AtualizarStatusLeadPayload) {
    return apiClient.patch<AtualizarStatusLeadResponse>(`/treinador/leads/${id}/status`, payload);
  },
  registrarInteracao(id: string, observacao: string) {
    return apiClient.post<RegistrarInteracaoLeadResponse>(`/treinador/leads/${id}/interacoes`, { observacao });
  },
  enviarConvite(id: string) {
    return apiClient.post<EnviarConviteLeadResponse>(`/treinador/leads/${id}/convite`, {});
  },
  metricas(inicio?: string, fim?: string) {
    return apiClient.get<ObterMetricasLeadsResponse>("/treinador/leads/metricas", { params: { inicio, fim } });
  },
};
