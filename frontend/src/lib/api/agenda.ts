import { apiClient } from "./client";
import type { BloqueioAgendaResponse, PoliticaAgendaResponse } from "@/types";

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
};
