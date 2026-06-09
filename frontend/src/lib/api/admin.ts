import { apiClient } from "./client";
import type {
  TreinadorResponse,
  PlanoPlataformaResponse,
  TierPlano,
  ExercicioResponse,
  GrupoMuscularResponse,
  PaginatedResponse,
  TreinadorStatus,
  AlunoResponse,
  AlunoStatus,
  MeuVinculoResponse,
  FichaAlunoResponse,
  ExecucaoTreinoResponse,
  ProgressaoAlunoResponse,
  VinculoDetalheResponse,
  VinculoStatus,
  TreinoResponse,
  PacoteResponse,
  HealthReportConfigResponse,
  AtualizarHealthReportConfigRequest,
  HealthSnapshotResponse,
} from "@/types";

export interface ListarExerciciosGlobaisResponse {
  items: ExercicioResponse[];
  total: number;
  pagina: number;
  tamanhoPagina: number;
}

export interface DashboardStatsResponse {
  planoDistribuicao: { tier: string; total: number }[];
  alunoFinalidade: { finalidade: string; total: number }[];
}

export interface ListTreinadoresParams {
  status?: TreinadorStatus;
  pagina?: number;
  tamanhoPagina?: number;
}

export const adminApi = {
  listTreinadores(params?: ListTreinadoresParams) {
    return apiClient.get<PaginatedResponse<TreinadorResponse>>("/admin/treinadores", { params });
  },

  getTreinador(treinadorId: string) {
    return apiClient.get<TreinadorResponse>(`/admin/treinadores/${treinadorId}`);
  },

  aprovarTreinador(treinadorId: string, observacao?: string | null) {
    return apiClient.post(`/admin/treinadores/${treinadorId}/aprovar`, { observacao: observacao ?? null });
  },

  reprovarTreinador(treinadorId: string, observacao?: string | null) {
    return apiClient.post(`/admin/treinadores/${treinadorId}/reprovar`, { observacao: observacao ?? null });
  },

  inativarTreinador(treinadorId: string, observacao?: string | null) {
    return apiClient.post(`/admin/treinadores/${treinadorId}/inativar`, { observacao: observacao ?? null });
  },

  excluirTreinador(treinadorId: string) {
    return apiClient.delete(`/admin/treinadores/${treinadorId}`);
  },

  atribuirPlano(treinadorId: string, planoId: string) {
    return apiClient.patch(`/admin/treinadores/${treinadorId}/plano`, { planoId });
  },

  listPlanos() {
    return apiClient.get<PlanoPlataformaResponse[]>("/admin/planos");
  },

  criarPlano(nome: string, tier: TierPlano, maxAlunos: number, preco: number, descricao?: string) {
    return apiClient.post<PlanoPlataformaResponse>("/admin/planos", { nome, tier, maxAlunos, preco, descricao });
  },

  atualizarPlano(planoId: string, data: { nome?: string; tier?: TierPlano; maxAlunos?: number; preco?: number; descricao?: string | null }) {
    return apiClient.patch<PlanoPlataformaResponse>(`/admin/planos/${planoId}`, data);
  },

  excluirPlano(planoId: string) {
    return apiClient.delete(`/admin/planos/${planoId}`);
  },

  listGruposMusculares() {
    return apiClient.get<GrupoMuscularResponse[]>("/admin/grupos-musculares");
  },

  criarGrupoMuscular(nome: string) {
    return apiClient.post<GrupoMuscularResponse>("/admin/grupos-musculares", { nome });
  },

  atualizarGrupoMuscular(id: string, nome: string) {
    return apiClient.patch<GrupoMuscularResponse>(`/admin/grupos-musculares/${id}`, { nome });
  },

  excluirGrupoMuscular(id: string) {
    return apiClient.delete(`/admin/grupos-musculares/${id}`);
  },

  listExerciciosGlobais(params?: { pagina?: number; tamanhoPagina?: number; nome?: string; grupoMuscularId?: string; ordenarPor?: string }) {
    return apiClient.get<ListarExerciciosGlobaisResponse>("/admin/exercicios", { params });
  },

  criarExercicioGlobal(data: { nome: string; grupoMuscularId: string; descricao?: string | null }) {
    return apiClient.post<ExercicioResponse>("/admin/exercicios", data);
  },

  atualizarExercicioGlobal(exercicioId: string, data: { nome?: string; grupoMuscularId?: string; descricao?: string | null }) {
    return apiClient.patch<ExercicioResponse>(`/admin/exercicios/${exercicioId}`, data);
  },

  excluirExercicioGlobal(exercicioId: string) {
    return apiClient.delete(`/admin/exercicios/${exercicioId}`);
  },

  // Alunos (visibilidade admin)
  listAlunos(params?: { nome?: string; status?: AlunoStatus; pagina?: number; tamanhoPagina?: number }) {
    return apiClient.get<PaginatedResponse<AlunoResponse>>("/admin/alunos", { params });
  },

  getAluno(alunoId: string) {
    return apiClient.get<AlunoResponse>(`/admin/alunos/${alunoId}`);
  },

  alterarStatusAluno(alunoId: string, status: AlunoStatus) {
    // Sem prefixo /admin de propósito: o backend mapeia esta rota no grupo /alunos
    // (AlunoEndpoints), apenas com policy SystemAdmin — não no grupo /admin.
    return apiClient.patch<AlunoResponse>(`/alunos/${alunoId}/status`, { status });
  },

  getAlunoVinculo(alunoId: string) {
    return apiClient.get<MeuVinculoResponse>(`/admin/alunos/${alunoId}/vinculo`);
  },

  getAlunoFichas(alunoId: string, params?: { pagina?: number; tamanhoPagina?: number }) {
    return apiClient.get<PaginatedResponse<FichaAlunoResponse>>(`/admin/alunos/${alunoId}/fichas`, { params });
  },

  getFichaDetalhe(treinoAlunoId: string) {
    return apiClient.get<FichaAlunoResponse>(`/admin/fichas/${treinoAlunoId}`);
  },

  getAlunoExecucoes(alunoId: string, params?: { pagina?: number; tamanhoPagina?: number }) {
    return apiClient.get<PaginatedResponse<ExecucaoTreinoResponse>>(`/admin/alunos/${alunoId}/execucoes`, { params });
  },

  getAlunoProgressao(alunoId: string, params?: { de?: string; ate?: string }) {
    return apiClient.get<ProgressaoAlunoResponse>(`/admin/alunos/${alunoId}/progressao`, { params });
  },

  // Sub-recursos de treinadores (visibilidade admin)
  getTreinadorAlunos(treinadorId: string, params?: { status?: AlunoStatus; pagina?: number; tamanhoPagina?: number }) {
    return apiClient.get<PaginatedResponse<AlunoResponse>>(`/admin/treinadores/${treinadorId}/alunos`, { params });
  },

  getTreinadorVinculos(treinadorId: string, params?: { status?: VinculoStatus; pagina?: number; tamanhoPagina?: number }) {
    return apiClient.get<PaginatedResponse<VinculoDetalheResponse>>(`/admin/treinadores/${treinadorId}/vinculos`, { params });
  },

  getTreinadorTreinos(treinadorId: string, params?: { pagina?: number; tamanhoPagina?: number; nome?: string; objetivo?: string }) {
    return apiClient.get<PaginatedResponse<TreinoResponse>>(`/admin/treinadores/${treinadorId}/treinos`, { params });
  },

  getTreino(treinoId: string) {
    return apiClient.get<TreinoResponse>(`/admin/treinos/${treinoId}`);
  },

  getTreinadorPacotes(treinadorId: string) {
    return apiClient.get<PacoteResponse[]>(`/admin/treinadores/${treinadorId}/pacotes`);
  },

  // Relatório de saúde
  getHealthReportConfig() {
    return apiClient.get<HealthReportConfigResponse>("/admin/health-report/config");
  },

  updateHealthReportConfig(data: AtualizarHealthReportConfigRequest) {
    return apiClient.put<HealthReportConfigResponse>("/admin/health-report/config", data);
  },

  listHealthSnapshots(params?: { limite?: number }) {
    return apiClient.get<HealthSnapshotResponse[]>("/admin/health-report/snapshots", { params });
  },

  runHealthReport() {
    return apiClient.post<HealthSnapshotResponse>("/admin/health-report/run");
  },

  getDashboardStats() {
    return apiClient.get<DashboardStatsResponse>("/admin/stats/dashboard");
  },

  // LGPD admin actions
  /**
   * Exporta dados pessoais de uma conta (treinador ou aluno) — portabilidade.
   */
  exportarDadosConta(contaId: string) {
    return apiClient.get(`/admin/contas/${contaId}/lgpd/exportar`, {
      responseType: "blob",
    });
  },

  /**
   * Anonimiza/exclui dados pessoais de uma conta — direito ao esquecimento.
   */
  anonimizarConta(contaId: string) {
    return apiClient.delete(`/admin/contas/${contaId}/lgpd`);
  },
};
