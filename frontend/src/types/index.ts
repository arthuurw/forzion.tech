export type TipoConta = "SystemAdmin" | "Treinador" | "Aluno";

export type AlunoStatus = "AguardandoAprovacao" | "Ativo" | "Inativo";
export type TreinadorStatus = "AguardandoPagamento" | "AguardandoAprovacao" | "Ativo" | "Inativo";
export type VinculoStatus = "AguardandoAprovacao" | "Ativo" | "Inativo";
export type TreinoAlunoStatus = "Ativo" | "Inativo";
// Deve espelhar exatamente o enum C# Domain/Enums/ObjetivoTreino (JsonStringEnumConverter
// casa por nome): valor fora da lista quebra a desserialização do POST /treinos.
export type ObjetivoTreino =
  | "Hipertrofia"
  | "Emagrecimento"
  | "Resistencia"
  | "Forca"
  | "Reabilitacao";

export type DificuldadeTreino = "Iniciante" | "Intermediario" | "Avancado";

export interface LoginResponse {
  token: string;
  refreshToken: string;
  tipoConta: TipoConta;
  contaId: string;
  perfilId: string;
  nome: string;
  mfaRequerido?: boolean;
  mfaPendingToken?: string | null;
  mfaPendingExpiraEm?: string | null;
}

export interface MfaPendingResult {
  mfaRequerido: true;
  mfaPendingExpiraEm: string | null;
}

export interface CompletarMfaResponse {
  login: LoginResponse;
  trustedDeviceToken: string | null;
}

// Resposta da rotação de sessão (/auth/refresh). Mesmo shape do login: novo access +
// refresh rotacionado (single-use). O refresh raw nunca chega ao JS — fica em cookie httpOnly.
export interface RefreshResponse {
  token: string;
  refreshToken: string;
  tipoConta: TipoConta;
  contaId: string;
  perfilId: string;
  nome: string;
}

// SessionUser nunca inclui o token — o token permanece em httpOnly cookie
// e não deve ser exposto ao JavaScript client-side.
export interface SessionUser {
  contaId: string;
  tipoConta: TipoConta;
  perfilId: string;
  nome: string;
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
  comoExecutar?: string | null;
  videoId?: string | null;
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
  comoExecutar?: string | null;
  videoId?: string | null;
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
export type PagamentoStatus = "Pendente" | "Pago" | "Expirado" | "Falhou" | "Estornado" | "EmDisputa";
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

export interface ListarPagamentosAssinaturaAlunoResponse {
  items: PagamentoResponse[];
  total: number;
  pagina: number;
  tamanhoPagina: number;
}

export interface OnboardingStatusResponse {
  onboardingCompleto: boolean;
  contaConfigurada: boolean;
  modoPagamentoAluno: ModoPagamentoAluno;
  modoPagamentoPodeAlterarEm: string | null;
}

export interface PreviewModoPagamentoResponse {
  assinaturasAtivasAlunos: number;
  vinculosCobravelSemAssinatura: number;
}

export interface RecebimentoTreinadorResponse {
  pagamentoId: string;
  bruto: number;
  taxaPercent: number | null;
  liquidoEstimado: number | null;
  status: PagamentoStatus;
  nomeAluno: string;
  metodo: MetodoPagamento;
  createdAt: string;
  dataPagamento: string | null;
}

export interface ListarRecebimentosTreinadorResultado {
  itens: RecebimentoTreinadorResponse[];
  proximoCursor: string | null;
  taxaPlataformaPercent: number;
}

export type AssinaturaTreinadorStatus = "Pendente" | "Ativa" | "Inadimplente" | "Cancelada";
export type TipoTrocaPlano = "Upgrade" | "Downgrade" | "InadimplenteRegularizacao" | "UpgradeImediato";

export interface AssinaturaTreinadorResponse {
  assinaturaId: string;
  status: AssinaturaTreinadorStatus;
  valor: number;
  planoPlataformaId: string;
  dataProximaCobranca: string;
  planoPlataformaIdAgendado: string | null;
}

export interface TrocarPlanoTreinadorResponse {
  tipo: TipoTrocaPlano;
  pagamentoId: string | null;
  valorPagamento: number | null;
  metodoPagamento: MetodoPagamento | null;
  pixQrCode: string | null;
  pixQrCodeUrl: string | null;
  pixExpiracao: string | null;
  clientSecret: string | null;
  dataEfetivacao: string | null;
}

export interface PagamentoTreinadorStatusResponse {
  pagamentoId: string;
  status: PagamentoStatus;
  valor: number;
  metodo: MetodoPagamento;
}

export type ModoPagamentoAluno = "Plataforma" | "Externo";

export interface IniciarPagamentoPlanoResponse {
  pagamentoId: string;
  valor: number;
  status: PagamentoStatus;
  metodoPagamento: MetodoPagamento;
  stripePaymentIntentId: string | null;
  pixQrCode: string | null;
  pixQrCodeUrl: string | null;
  pixExpiracao: string | null;
  clientSecret: string | null;
  createdAt: string;
}

// Erro RFC 7807
export interface ProblemDetails {
  title: string;
  detail?: string;
  status: number;
  errors?: Record<string, string[]>;
  code?: string;
}

export type StatusSaude = "Ok" | "Degradado" | "Falha";

export interface HealthReportConfigResponse {
  id: string;
  ativo: boolean;
  horaEnvioUtc: string;
  destinatarios: string[];
  incluirLiveness: boolean;
  incluirKpis: boolean;
  incluirEntregabilidade: boolean;
  incluirErros: boolean;
  ultimoEnvioEm: string | null;
}

export interface AtualizarHealthReportConfigRequest {
  ativo: boolean;
  horaEnvioUtc: string;
  destinatarios: string[];
  incluirLiveness: boolean;
  incluirKpis: boolean;
  incluirEntregabilidade: boolean;
  incluirErros: boolean;
}

export interface HealthSnapshotResponse {
  id: string;
  capturadoEm: string;
  ambiente: string;
  statusGeral: StatusSaude;
  payloadJson: string;
}
