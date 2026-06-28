import { apiClient } from "./client";
import type { AlunoDashboardResponse, AlunoResponse, ExecucaoTreinoResponse, MeuVinculoResponse, ObjetivoTreino, ProgressaoAlunoResponse, TreinoAlunoStatus, TreinoExercicioResponse, PaginatedResponse, VinculoResponse } from "@/types";

export interface AtualizarAnamneseData {
  diasDisponiveis: number | null;
  tempoDisponivelMinutos: number | null;
  finalidade: string | null;
  focoTreino: string | null;
  nivelCondicionamento: string | null;
  limitacoesFisicas: string | null;
  doencas: string | null;
  observacoesAdicionais: string | null;
  consentimentoDadosSaude: boolean;
  consentimentoDadosSaudeEm: string | null;
}

export interface TreinoAlunoDetalheResponse {
  treinoAlunoId: string;
  treinoId: string;
  nomeTreino: string;
  objetivo: ObjetivoTreino;
  status: TreinoAlunoStatus;
  exercicios: TreinoExercicioResponse[];
}

export interface ExecucaoExercicioData {
  treinoExercicioId: string;
  seriesExecutadas: number;
  repeticoesExecutadas: number;
  cargaExecutada?: number | null;
  observacao?: string | null;
}

export interface CriarExecucaoData {
  treinoId: string;
  dataExecucao: string;
  observacao?: string | null;
  exercicios: ExecucaoExercicioData[];
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
  criarExecucao(data: CriarExecucaoData, opts?: { idempotencyKey?: string }) {
    return opts?.idempotencyKey
      ? apiClient.post<ExecucaoTreinoResponse>("/aluno/execucoes", data, {
          headers: { "Idempotency-Key": opts.idempotencyKey },
        })
      : apiClient.post<ExecucaoTreinoResponse>("/aluno/execucoes", data);
  },

  getMinhaProgressao(de?: string, ate?: string) {
    return apiClient.get<ProgressaoAlunoResponse>("/aluno/progressao", { params: { de, ate } });
  },

  getMeuVinculo() {
    return apiClient.get<MeuVinculoResponse>("/aluno/vinculo");
  },

  solicitarTrocaTreinador(novoTreinadorId: string, pacoteId: string) {
    return apiClient.post<VinculoResponse>("/aluno/troca-treinador", { novoTreinadorId, pacoteId });
  },

  getDashboard() {
    return apiClient.get<AlunoDashboardResponse>("/aluno/dashboard");
  },

  getMeuPerfilAluno(alunoId: string) {
    return apiClient.get<AlunoResponse>(`/alunos/${alunoId}`);
  },

  atualizarAnamnese(data: AtualizarAnamneseData) {
    return apiClient.put<AlunoResponse>("/aluno/anamnese", data);
  },
};
