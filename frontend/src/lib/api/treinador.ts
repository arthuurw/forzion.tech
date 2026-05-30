import { apiClient } from "./client";
import type {
  AlunoResponse,
  VinculoDetalheResponse,
  VinculoStatus,
  TreinoResponse,
  TreinoAlunoResponse,
  TreinoAlunoVinculado,
  ExercicioResponse,
  PacoteResponse,
  GrupoMuscularResponse,
  PaginatedResponse,
  AlunoStatus,
  ObjetivoTreino,
  DificuldadeTreino,
  ProgressaoAlunoResponse,
} from "@/types";

export interface CriarFichaData {
  alunoId?: string | null;
  nome: string;
  objetivo: ObjetivoTreino;
  dificuldade?: DificuldadeTreino;
  dataInicio?: string | null;
  dataFim?: string | null;
}

export interface SerieConfigData {
  quantidade: number;
  repeticoesMin: number;
  repeticoesMax?: number | null;
  descricao?: string | null;
  carga?: number | null;
  descanso?: number | null;
}

export interface AdicionarExercicioData {
  exercicioId: string;
  series: SerieConfigData[];
}

export interface CriarExercicioData {
  nome: string;
  descricao?: string | null;
  grupoMuscularId: string;
}

export interface CriarPacoteData {
  nome: string;
  preco: number;
  descricao?: string | null;
}

export const treinadorApi = {
  listAlunos(params?: { status?: AlunoStatus; pagina?: number; tamanhoPagina?: number }) {
    return apiClient.get<PaginatedResponse<AlunoResponse>>("/treinador/alunos", { params });
  },
  getAluno(alunoId: string) {
    return apiClient.get<AlunoResponse>(`/treinador/alunos/${alunoId}`);
  },
  getFichasDoAluno(alunoId: string) {
    return apiClient.get<TreinoAlunoResponse[]>(`/treinador/alunos/${alunoId}/fichas`);
  },
  getProgressaoAluno(alunoId: string, params?: { de?: string; ate?: string }) {
    return apiClient.get<ProgressaoAlunoResponse>(`/treinador/alunos/${alunoId}/progressao`, { params });
  },

  listVinculos(params?: { status?: VinculoStatus; pagina?: number; tamanhoPagina?: number }) {
    return apiClient.get<PaginatedResponse<VinculoDetalheResponse>>("/treinador/vinculos", { params });
  },
  aprovarVinculo(vinculoId: string, pacoteId: string, trarFichas = false) {
    return apiClient.post(`/treinador/vinculos/${vinculoId}/aprovar`, { pacoteId, trarFichas });
  },
  desvincularAluno(vinculoId: string, observacao?: string | null) {
    return apiClient.post(`/treinador/vinculos/${vinculoId}/desvincular`, { observacao: observacao ?? null });
  },

  listFichas(params?: { pagina?: number; tamanhoPagina?: number; nome?: string; objetivo?: string; ordenarPor?: string }) {
    return apiClient.get<PaginatedResponse<TreinoResponse>>("/treinador/treinos", { params });
  },
  getFicha(treinoId: string) {
    return apiClient.get<TreinoResponse>(`/treinos/${treinoId}`);
  },
  criarFicha(data: CriarFichaData) {
    return apiClient.post<TreinoResponse>("/treinos", data);
  },
  atualizarFicha(treinoId: string, data: { nome?: string; objetivo?: ObjetivoTreino; dificuldade?: DificuldadeTreino; dataInicio?: string | null; dataFim?: string | null; limparDataInicio?: boolean; limparDataFim?: boolean }) {
    return apiClient.patch<TreinoResponse>(`/treinos/${treinoId}`, data);
  },
  excluirFicha(treinoId: string) {
    return apiClient.delete(`/treinos/${treinoId}`);
  },
  duplicarFicha(treinoId: string) {
    return apiClient.post<TreinoResponse>(`/treinos/${treinoId}/duplicar`);
  },
  vincularFichaAoAluno(alunoId: string, treinoId: string) {
    return apiClient.post(`/treinador/alunos/${alunoId}/fichas/${treinoId}`);
  },
  listAlunosVinculados(treinoId: string) {
    return apiClient.get<TreinoAlunoVinculado[]>(`/treinos/${treinoId}/alunos`);
  },

  adicionarExercicio(treinoId: string, data: AdicionarExercicioData) {
    return apiClient.post(`/treinos/${treinoId}/exercicios`, data);
  },
  removerExercicio(treinoId: string, exercicioId: string) {
    return apiClient.delete(`/treinos/${treinoId}/exercicios/${exercicioId}`);
  },
  editarExercicioTreino(treinoId: string, treinoExercicioId: string, data: { series: SerieConfigData[] }) {
    return apiClient.put<TreinoResponse>(`/treinos/${treinoId}/exercicios/${treinoExercicioId}`, data);
  },
  atualizarObservacaoExercicio(treinoId: string, treinoExercicioId: string, observacao: string | null) {
    return apiClient.patch<TreinoResponse>(`/treinos/${treinoId}/exercicios/${treinoExercicioId}/observacao`, { observacao });
  },

  listGruposMusculares() {
    return apiClient.get<GrupoMuscularResponse[]>("/treinador/grupos-musculares");
  },

  listExercicios(params?: { global?: boolean; pagina?: number; tamanhoPagina?: number; nome?: string; grupoMuscularId?: string; ordenarPor?: string }) {
    return apiClient.get<PaginatedResponse<ExercicioResponse>>("/treinador/exercicios", { params });
  },
  criarExercicio(data: CriarExercicioData) {
    return apiClient.post<ExercicioResponse>("/treinador/exercicios", data);
  },
  copiarExercicioGlobal(exercicioId: string) {
    return apiClient.post<ExercicioResponse>(`/treinador/exercicios/${exercicioId}/copiar`);
  },
  atualizarExercicio(exercicioId: string, data: { nome?: string; grupoMuscularId?: string; descricao?: string | null }) {
    return apiClient.patch<ExercicioResponse>(`/treinador/exercicios/${exercicioId}`, data);
  },
  excluirExercicio(exercicioId: string) {
    return apiClient.delete(`/treinador/exercicios/${exercicioId}`);
  },

  reativarAluno(alunoId: string, pacoteId: string) {
    return apiClient.post<VinculoDetalheResponse>(`/treinador/alunos/${alunoId}/reativar`, { pacoteId });
  },

  listPacotes() {
    return apiClient.get<PacoteResponse[]>("/treinador/pacotes");
  },
  criarPacote(data: CriarPacoteData) {
    return apiClient.post<PacoteResponse>("/treinador/pacotes", data);
  },
  atualizarPacote(pacoteId: string, data: { nome?: string; preco?: number; descricao?: string | null }) {
    return apiClient.patch<PacoteResponse>(`/treinador/pacotes/${pacoteId}`, data);
  },
  excluirPacote(pacoteId: string) {
    return apiClient.delete(`/treinador/pacotes/${pacoteId}`);
  },
};
