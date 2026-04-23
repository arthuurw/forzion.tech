import { apiClient } from "./client";
import type { ExecucaoTreinoResponse, MeuVinculoResponse, ObjetivoTreino, TreinoAlunoStatus, TreinoExercicioResponse, PaginatedResponse, VinculoResponse } from "@/types";

export interface TreinoAlunoDetalheResponse {
  treinoAlunoId: string;
  treinoId: string;
  nomeTreino: string;
  objetivo: ObjetivoTreino;
  status: TreinoAlunoStatus;
  exercicios: TreinoExercicioResponse[];
}

export interface CriarExecucaoData {
  treinoId: string;
  dataExecucao: string;
  observacao?: string | null;
  exercicios: never[];
}

export const alunoApi = {
  listFichas(params?: { pagina?: number; tamanhoPagina?: number }) {
    return apiClient.get<PaginatedResponse<TreinoAlunoDetalheResponse>>("/aluno/fichas", { params });
  },
  getFicha(treinoAlunoId: string) {
    return apiClient.get<TreinoAlunoDetalheResponse>(`/aluno/fichas/${treinoAlunoId}`);
  },
  listExecucoes(params?: { pagina?: number; tamanhoPagina?: number }) {
    return apiClient.get<PaginatedResponse<ExecucaoTreinoResponse>>("/aluno/execucoes", { params });
  },
  criarExecucao(data: CriarExecucaoData) {
    return apiClient.post<ExecucaoTreinoResponse>("/aluno/execucoes", data);
  },

  getMeuVinculo() {
    return apiClient.get<MeuVinculoResponse>("/aluno/vinculo");
  },

  solicitarTrocaTreinador(novoTreinadorId: string, pacoteId: string) {
    return apiClient.post<VinculoResponse>("/aluno/troca-treinador", { novoTreinadorId, pacoteId });
  },
};
