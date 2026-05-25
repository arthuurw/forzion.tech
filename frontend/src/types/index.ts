export type TipoConta = "SystemAdmin" | "Treinador" | "Aluno";

export type AlunoStatus = "AguardandoAprovacao" | "Ativo" | "Inativo";
export type TreinadorStatus = "AguardandoAprovacao" | "Ativo" | "Inativo";
export type VinculoStatus = "AguardandoAprovacao" | "Ativo" | "Inativo";
export type TreinoAlunoStatus = "Ativo" | "Inativo";
export type ObjetivoTreino =
  | "Hipertrofia"
  | "Emagrecimento"
  | "Resistencia"
  | "Forca"
  | "Flexibilidade"
  | "Condicionamento";

export type DificuldadeTreino = "Iniciante" | "Intermediario" | "Avancado";

export interface LoginResponse {
  token: string;
  tipoConta: TipoConta;
  contaId: string;
  perfilId: string;
}

// SessionUser nunca inclui o token — o token permanece em httpOnly cookie
// e não deve ser exposto ao JavaScript client-side.
export interface SessionUser {
  contaId: string;
  tipoConta: TipoConta;
  perfilId: string;
}

export type FinalidadeTreino =
  | "Hipertrofia"
  | "Emagrecimento"
  | "CondicionamentoFisico"
  | "Saude"
  | "PerformanceEsportiva"
  | "Reabilitacao"
  | "Outro";

export type NivelCondicionamento = "Sedentario" | "Iniciante" | "Intermediario" | "Avancado";

export type TempoDisponivel =
  | "TrintaMinutos"
  | "QuarentaCincoMinutos"
  | "UmaHora"
  | "UmaHoraETrinta"
  | "DuasHoras";

export interface AlunoResponse {
  alunoId: string;
  nome: string;
  email: string | null;
  telefone: string | null;
  status: AlunoStatus;
  contaId: string;
  createdAt: string;
  updatedAt: string | null;
  diasDisponiveis: number | null;
  tempoDisponivelMinutos: TempoDisponivel | null;
  finalidade: FinalidadeTreino | null;
  focoTreino: string | null;
  nivelCondicionamento: NivelCondicionamento | null;
  limitacoesFisicas: string | null;
  doencas: string | null;
  observacoesAdicionais: string | null;
}

export interface TreinadorResponse {
  treinadorId: string;
  nome: string;
  contaId: string;
  status: TreinadorStatus;
  planoPlataformaId: string | null;
  createdAt: string;
}

export interface VinculoResponse {
  vinculoId: string;
  treinadorId: string;
  alunoId: string;
  pacoteId: string | null;
  status: VinculoStatus;
  createdAt: string;
}

export interface VinculoDetalheResponse extends VinculoResponse {
  nomeAluno: string;
  emailAluno: string | null;
  temVinculoAtivoPrevio: boolean;
}

// Ficha vinculada ao aluno (visão do treinador)
export interface TreinoAlunoResponse {
  treinoAlunoId: string;
  treinoId: string;
  nomeTreino: string;
  status: TreinoAlunoStatus;
}

export interface SerieConfigResponse {
  serieConfigId: string;
  quantidade: number;
  repeticoesMin: number;
  repeticoesMax: number | null;
  descricao: string | null;
  carga: number | null;
  descanso: number | null;
  ordem: number;
}

export interface TreinoExercicioResponse {
  treinoExercicioId: string;
  exercicioId: string;
  nomeExercicio: string;
  series: SerieConfigResponse[];
  ordem: number;
  observacao?: string | null;
}

export interface TreinoResponse {
  treinoId: string;
  nome: string;
  objetivo: ObjetivoTreino;
  dificuldade: DificuldadeTreino;
  dataInicio: string | null;
  dataFim: string | null;
  treinadorId: string;
  exercicios: TreinoExercicioResponse[];
  createdAt: string;
  updatedAt: string | null;
  nomeAluno?: string | null;
}

export interface ExercicioResponse {
  exercicioId: string;
  nome: string;
  descricao: string | null;
  grupoMuscularId: string;
  grupoMuscular: string | null;
  treinadorId: string | null;
  isGlobal: boolean;
}

export type TierPlano = "Free" | "Basic" | "Pro" | "ProPlus" | "Elite";

export interface PlanoPlataformaResponse {
  planoId: string;
  nome: string;
  tier: TierPlano;
  descricao: string | null;
  maxAlunos: number;
  preco: number;
  isAtivo?: boolean;
  createdAt?: string;
  updatedAt?: string | null;
}

// Grupo Muscular
export interface GrupoMuscularResponse {
  id: string;
  nome: string;
  createdAt: string;
  updatedAt: string | null;
}

export interface PacoteResponse {
  pacoteId: string;
  nome: string;
  descricao: string | null;
  preco: number;
  treinadorId: string;
  isAtivo?: boolean;
  createdAt?: string;
  updatedAt?: string | null;
}

export interface TreinoAlunoVinculado {
  treinoAlunoId: string;
  alunoId: string;
  nomeAluno: string;
  status: string;
}

export interface FichaAlunoResponse {
  treinoAlunoId: string;
  treinoId: string;
  nomeTreino: string;
  objetivo: ObjetivoTreino;
  status: TreinoAlunoStatus;
  exercicios: TreinoExercicioResponse[];
}

export interface ExecucaoTreinoResponse {
  execucaoId: string;
  treinoId: string;
  alunoId: string;
  dataExecucao: string;
  observacao: string | null;
  createdAt: string;
  nomeTreino: string;
  totalExercicios: number;
  totalSeries: number;
}

export interface VinculoAlunoItemResponse {
  vinculoId: string;
  treinadorId: string;
  nomeTreinador: string;
  status: VinculoStatus;
  dataInicio: string | null;
  createdAt: string;
}

export interface MeuVinculoResponse {
  vinculoAtivo: VinculoAlunoItemResponse | null;
  vinculoPendente: VinculoAlunoItemResponse | null;
}

export interface PaginatedResponse<T> {
  items: T[];
  total: number;
  pagina: number;
  tamanhoPagina: number;
}

export interface PontoProgressao {
  data: string;
  cargaMaxima: number | null;
  seriesExecutadas: number;
  repeticoesExecutadas: number;
}

export interface ExercicioProgressao {
  nomeExercicio: string;
  grupoMuscular: string;
  historico: PontoProgressao[];
}

export interface ProgressaoAlunoResponse {
  exercicios: ExercicioProgressao[];
}

export type AssinaturaAlunoStatus = "Pendente" | "Ativa" | "Inadimplente" | "Cancelada";
export type PagamentoStatus = "Pendente" | "Pago" | "Expirado" | "Falhou";
export type MetodoPagamento = "Pix" | "Cartao";

export interface AssinaturaAlunoResponse {
  assinaturaAlunoId: string;
  vinculoId: string;
  pacoteId: string;
  treinadorId: string;
  alunoId: string;
  valor: number;
  status: AssinaturaAlunoStatus;
  dataInicio: string;
  dataProximaCobranca: string;
  dataCancelamento: string | null;
  createdAt: string;
}

export interface PagamentoResponse {
  pagamentoId: string;
  assinaturaAlunoId: string;
  valor: number;
  status: PagamentoStatus;
  metodoPagamento: MetodoPagamento;
  pixQrCode: string | null;
  pixQrCodeUrl: string | null;
  pixExpiracao: string | null;
  clientSecret: string | null;
  dataPagamento: string | null;
  createdAt: string;
}

export interface OnboardingStatusResponse {
  onboardingCompleto: boolean;
  contaConfigurada: boolean;
}

// Erro RFC 7807
export interface ProblemDetails {
  title: string;
  detail?: string;
  status: number;
  errors?: Record<string, string[]>;
}
