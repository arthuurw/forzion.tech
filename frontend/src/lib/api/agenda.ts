import { apiClient } from "./client";
import type {
  BloqueioAgendaResponse,
  PoliticaAgendaResponse,
  ListarSolicitacoesResponse,
  SolicitacaoAgendamentoStatus,
} from "@/types";

export interface CriarBloqueioPontualPayload {
  tipo: "Pontual";
  inicioUtc: string;
  fimUtc: string;
  motivo?: string | null;
}

export interface CriarBloqueioRecorrentePayload {
  tipo: "RecorrenteSemanal";
  diaSemana: number;
  horaInicio: string;
  horaFim: string;
  motivo?: string | null;
}

export type CriarBloqueioAgendaPayload = CriarBloqueioPontualPayload | CriarBloqueioRecorrentePayload;

export interface AtualizarPoliticaAgendaPayload {
  antecedenciaMinimaHoras: number;
  horizonteDias: number;
}

export interface ListarSolicitacoesParams {
  status?: SolicitacaoAgendamentoStatus;
  pagina?: number;
  tamanhoPagina?: number;
}

export const agendaApi = {
  listarBloqueios() {
    return apiClient.get<BloqueioAgendaResponse[]>("/treinador/agenda/bloqueios");
  },
  criarBloqueio(payload: CriarBloqueioAgendaPayload) {
    return apiClient.post<BloqueioAgendaResponse>("/treinador/agenda/bloqueios", payload);
  },
  apagarBloqueio(id: string) {
    return apiClient.delete(`/treinador/agenda/bloqueios/${id}`);
  },
  obterPolitica() {
    return apiClient.get<PoliticaAgendaResponse>("/treinador/agenda/politica");
  },
  atualizarPolitica(payload: AtualizarPoliticaAgendaPayload) {
    return apiClient.put<PoliticaAgendaResponse>("/treinador/agenda/politica", payload);
  },
  listarSolicitacoes(params: ListarSolicitacoesParams = {}) {
    return apiClient.get<ListarSolicitacoesResponse>("/treinador/agenda/solicitacoes", { params });
  },
  confirmarSolicitacao(id: string) {
    return apiClient.post<void>(`/treinador/agenda/solicitacoes/${id}/confirmar`, {});
  },
  recusarSolicitacao(id: string, motivo?: string | null) {
    return apiClient.post<void>(`/treinador/agenda/solicitacoes/${id}/recusar`, { motivo: motivo ?? null });
  },
  cancelarSolicitacao(id: string, motivo?: string | null) {
    return apiClient.post<void>(`/treinador/agenda/solicitacoes/${id}/cancelar`, { motivo: motivo ?? null });
  },
};
