"use client";
import { useCallback, useEffect, useMemo, useState } from "react";
import dynamic from "next/dynamic";
import { useParams, useRouter } from "next/navigation";
import {
  Box, Typography, Card, CardContent, Stack, Chip,
  Tab, Tabs, ToggleButtonGroup, ToggleButton, Grid, Skeleton, Button,
} from "@mui/material";
import StatusChip from "@/components/ui/StatusChip";
import AlertBanner from "@/components/ui/AlertBanner";
import PageHeader from "@/components/ui/PageHeader";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import DataList from "@/components/ui/DataList";
import InfoLine from "@/components/ui/InfoLine";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import type { Column } from "@/components/ui/ResponsiveTable";
import { adminApi } from "@/lib/api/admin";
import { extractApiError } from "@/lib/api/extractApiError";
import type {
  AlunoResponse, MeuVinculoResponse, FichaAlunoResponse,
  ExecucaoTreinoResponse, ExercicioProgressao,
} from "@/types";
import { usePaginatedList } from "@/hooks/usePaginatedList";
import { OBJETIVO_LABEL, FINALIDADE_LABEL, NIVEL_LABEL, TEMPO_LABEL } from "@/lib/constants/labels";
import { periodoParaDatas, formatarData, formatarTelefone } from "@/lib/utils/formatting";

const ProgressaoCargaChart = dynamic(
  () => import("./_charts/ProgressaoCargaChart"),
  { ssr: false, loading: () => null },
);

type Periodo = "7d" | "30d" | "60d" | "90d";

const PERIODOS: { value: Periodo; label: string }[] = [
  { value: "7d",  label: "7 dias"  },
  { value: "30d", label: "30 dias" },
  { value: "60d", label: "60 dias" },
  { value: "90d", label: "90 dias" },
];

const FICHAS_COLUMNS: Column[] = [
  { label: "Ficha" },
  { label: "Objetivo" },
  { label: "Status" },
];

const EXEC_COLUMNS: Column[] = [
  { label: "Data" },
  { label: "Treino" },
  { label: "Exercícios" },
  { label: "Observação" },
];

export default function DetalheAlunoAdminPage() {
  const { alunoId } = useParams<{ alunoId: string }>();
  const router = useRouter();

  const [aluno, setAluno] = useState<AlunoResponse | null>(null);
  const [vinculo, setVinculo] = useState<MeuVinculoResponse | null>(null);
  const [loadingHeader, setLoadingHeader] = useState(true);
  const [error, setError] = useState("");
  const [tab, setTab] = useState(0);

  const [exportingLgpd, setExportingLgpd] = useState(false);
  const [anonimizarDialog, setAnonimizarDialog] = useState(false);
  const [anonimizando, setAnonimizando] = useState(false);

  const [periodo, setPeriodo] = useState<Periodo>("30d");
  const [exercicios, setExercicios] = useState<ExercicioProgressao[]>([]);
  const [progLoading, setProgLoading] = useState(false);
  const [progLoaded, setProgLoaded] = useState(false);

  const loadHeader = useCallback(async () => {
    setLoadingHeader(true);
    try {
      const [alunoRes, vinculoRes] = await Promise.all([
        adminApi.getAluno(alunoId),
        adminApi.getAlunoVinculo(alunoId),
      ]);
      setAluno(alunoRes.data);
      setVinculo(vinculoRes.data);
    } catch (err) {
      setError(extractApiError(err, "Erro ao carregar dados do aluno."));
    } finally {
      setLoadingHeader(false);
    }
  }, [alunoId]);

  useEffect(() => { loadHeader(); }, [loadHeader]);

  const fichasFetcher = useCallback(
    (p: number, ps: number) =>
      adminApi.getAlunoFichas(alunoId, { pagina: p + 1, tamanhoPagina: ps }).then((r) => r.data),
    [alunoId]
  );

  const execFetcher = useCallback(
    (p: number, ps: number) =>
      adminApi.getAlunoExecucoes(alunoId, { pagina: p + 1, tamanhoPagina: ps }).then((r) => r.data),
    [alunoId]
  );

  const fichasList = usePaginatedList<FichaAlunoResponse>({ fetcher: fichasFetcher, errorMessage: "Erro ao carregar fichas." });
  const execList = usePaginatedList<ExecucaoTreinoResponse>({ fetcher: execFetcher, errorMessage: "Erro ao carregar execuções." });

  const loadProgressao = useCallback(async () => {
    setProgLoading(true);
    try {
      const { de, ate } = periodoParaDatas(periodo);
      const res = await adminApi.getAlunoProgressao(alunoId, { de, ate });
      setExercicios(res.data.exercicios);
    } catch {
      setExercicios([]);
    } finally {
      setProgLoading(false);
      setProgLoaded(true);
    }
  }, [alunoId, periodo]);

  useEffect(() => {
    if (tab === 3 && !progLoaded) loadProgressao();
  }, [tab, progLoaded, loadProgressao]);

  useEffect(() => {
    if (tab === 3 && progLoaded) loadProgressao();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [periodo]);

  const temPerfilTreino = useMemo(() => aluno && (
    aluno.finalidade || aluno.nivelCondicionamento || aluno.diasDisponiveis ||
    aluno.focoTreino || aluno.limitacoesFisicas || aluno.doencas || aluno.observacoesAdicionais
  ), [aluno]);

  const handleExportarLgpd = async () => {
    if (!aluno?.contaId) return;
    setExportingLgpd(true);
    try {
      const res = await adminApi.exportarDadosConta(aluno.contaId);
      const url = URL.createObjectURL(res.data as Blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `dados-${aluno.contaId}.json`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      setError(extractApiError(err, "Erro ao exportar dados."));
    } finally {
      setExportingLgpd(false);
    }
  };

  const handleAnonimizar = async () => {
    if (!aluno?.contaId) return;
    setAnonimizando(true);
    try {
      await adminApi.anonimizarConta(aluno.contaId);
      setAnonimizarDialog(false);
      router.push("/admin/alunos");
    } catch (err) {
      setError(extractApiError(err, "Erro ao anonimizar conta."));
      setAnonimizarDialog(false);
    } finally {
      setAnonimizando(false);
    }
  };

  if (loadingHeader) return <LoadingSpinner />;

  return (
    <Box>
      <PageHeader
        title={aluno?.nome ?? "Aluno"}
        backHref="/admin/alunos"
        action={aluno ? <StatusChip status={aluno.status} /> : undefined}
      />

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      <Tabs value={tab} onChange={(_, v) => setTab(v)} variant="scrollable" scrollButtons="auto" allowScrollButtonsMobile sx={{ mb: 3, borderBottom: 1, borderColor: "divider" }}>
        <Tab label="Dados" />
        <Tab label="Fichas" />
        <Tab label="Execuções" />
        <Tab label="Progressão" />
        <Tab label="LGPD" />
      </Tabs>

      {tab === 0 && aluno && (
        <Box>
          <Card variant="outlined" sx={{ mb: 2 }}>
            <CardContent sx={{ py: 1.5, "&:last-child": { pb: 1.5 } }}>
              <Stack spacing={0.75}>
                {aluno.email && <InfoLine label="E-mail" value={aluno.email} />}
                {aluno.telefone && <InfoLine label="Celular" value={formatarTelefone(aluno.telefone)} />}
                <InfoLine label="Cadastro" value={new Date(aluno.createdAt).toLocaleDateString("pt-BR")} />
              </Stack>
            </CardContent>
          </Card>

          {temPerfilTreino && (
            <Card variant="outlined" sx={{ mb: 2 }}>
              <CardContent>
                <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 1.5 }}>
                  Perfil de treino
                </Typography>
                <Stack spacing={0.75}>
                  {aluno.finalidade && <InfoLine label="Finalidade" value={FINALIDADE_LABEL[aluno.finalidade]} />}
                  {aluno.nivelCondicionamento && <InfoLine label="Nível" value={NIVEL_LABEL[aluno.nivelCondicionamento]} />}
                  {(aluno.diasDisponiveis || aluno.tempoDisponivelMinutos) && (
                    <Typography variant="body2">
                      <strong>Disponibilidade:</strong>{" "}
                      {aluno.diasDisponiveis ? `${aluno.diasDisponiveis} dias/semana` : ""}
                      {aluno.diasDisponiveis && aluno.tempoDisponivelMinutos ? " · " : ""}
                      {aluno.tempoDisponivelMinutos ? TEMPO_LABEL[aluno.tempoDisponivelMinutos] + "/dia" : ""}
                    </Typography>
                  )}
                  {aluno.focoTreino && <InfoLine label="Foco" value={aluno.focoTreino} />}
                  {aluno.limitacoesFisicas && <InfoLine label="Limitações físicas" value={aluno.limitacoesFisicas} />}
                  {aluno.doencas && <InfoLine label="Doenças / condições" value={aluno.doencas} />}
                  {aluno.observacoesAdicionais && <InfoLine label="Observações" value={aluno.observacoesAdicionais} />}
                </Stack>
              </CardContent>
            </Card>
          )}

          <Typography variant="h6" sx={{ fontWeight: 600, mb: 1.5 }}>Vínculo</Typography>
          <Card variant="outlined">
            <CardContent sx={{ py: 1.5, "&:last-child": { pb: 1.5 } }}>
              {vinculo?.vinculoAtivo ? (
                <Stack spacing={0.75}>
                  <InfoLine label="Treinador" value={vinculo.vinculoAtivo.nomeTreinador} />
                  <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                    <Typography variant="body2"><strong>Status:</strong></Typography>
                    <StatusChip status={vinculo.vinculoAtivo.status} />
                  </Box>
                  {vinculo.vinculoAtivo.dataInicio && (
                    <InfoLine label="Início" value={new Date(vinculo.vinculoAtivo.dataInicio).toLocaleDateString("pt-BR")} />
                  )}
                </Stack>
              ) : vinculo?.vinculoPendente ? (
                <Stack spacing={0.75}>
                  <InfoLine label="Treinador (pendente)" value={vinculo.vinculoPendente.nomeTreinador} />
                  <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                    <Typography variant="body2"><strong>Status:</strong></Typography>
                    <StatusChip status={vinculo.vinculoPendente.status} />
                  </Box>
                </Stack>
              ) : (
                <Typography variant="body2" color="text.secondary">Sem vínculo ativo.</Typography>
              )}
            </CardContent>
          </Card>
        </Box>
      )}

      {tab === 1 && (
        <DataList
          loading={fichasList.loading}
          items={fichasList.items}
          emptyMessage="Nenhuma ficha vinculada a este aluno."
          columns={FICHAS_COLUMNS}
          rowKey={(f) => f.treinoAlunoId}
          pagination={{
            count: fichasList.total,
            page: fichasList.page,
            rowsPerPage: fichasList.pageSize,
            onPageChange: fichasList.setPage,
            onRowsPerPageChange: fichasList.setPageSize,
          }}
          renderCell={(f, i) => {
            if (i === 0) return <Typography variant="body2" sx={{ fontWeight: 500 }}>{f.nomeTreino}</Typography>;
            if (i === 1) return <Typography variant="body2">{OBJETIVO_LABEL[f.objetivo] ?? f.objetivo}</Typography>;
            return <StatusChip status={f.status} />;
          }}
        />
      )}

      {tab === 2 && (
        <DataList
          loading={execList.loading}
          items={execList.items}
          emptyMessage="Nenhuma execução registrada para este aluno."
          columns={EXEC_COLUMNS}
          rowKey={(ex) => ex.execucaoId}
          pagination={{
            count: execList.total,
            page: execList.page,
            rowsPerPage: execList.pageSize,
            onPageChange: execList.setPage,
            onRowsPerPageChange: execList.setPageSize,
          }}
          renderCell={(ex, i) => {
            if (i === 0) return (
              <Typography variant="body2">
                {new Date(ex.dataExecucao).toLocaleDateString("pt-BR", {
                  weekday: "short", day: "2-digit", month: "short", year: "numeric",
                })}
              </Typography>
            );
            if (i === 1) return <Typography variant="body2" sx={{ fontWeight: 500 }}>{ex.nomeTreino}</Typography>;
            if (i === 2) return (
              <Stack direction="row" spacing={1}>
                <Chip size="small" label={`${ex.totalExercicios} exerc.`} variant="outlined" />
                <Chip size="small" label={`${ex.totalSeries} séries`} variant="outlined" />
              </Stack>
            );
            return <Typography variant="body2" color="text.secondary">{ex.observacao ?? "—"}</Typography>;
          }}
        />
      )}

      {tab === 3 && (
        <Box>
          <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 2, flexWrap: "wrap", gap: 1 }}>
            <Typography variant="h6" sx={{ fontWeight: 600 }}>Progressão por exercício</Typography>
            <ToggleButtonGroup
              value={periodo}
              exclusive
              onChange={(_, val) => { if (val) setPeriodo(val as Periodo); }}
              size="small"
            >
              {PERIODOS.map((p) => (
                <ToggleButton key={p.value} value={p.value} sx={{ fontSize: "0.75rem", px: 1.5 }}>
                  {p.label}
                </ToggleButton>
              ))}
            </ToggleButtonGroup>
          </Box>

          {progLoading ? (
            <Grid container spacing={2}>
              {[1, 2, 3].map((i) => (
                <Grid key={i} size={{ xs: 12, sm: 6, md: 4 }}>
                  <Skeleton variant="rectangular" height={200} sx={{ borderRadius: 1 }} />
                </Grid>
              ))}
            </Grid>
          ) : exercicios.length === 0 ? (
            <EmptyState message="Nenhuma execução registrada no período." />
          ) : (
            <Grid container spacing={2}>
              {exercicios.map((ex) => {
                const chartData = ex.historico.map((p) => ({
                  data: formatarData(p.data),
                  carga: p.cargaMaxima,
                }));
                const ultima = ex.historico.at(-1);
                return (
                  <Grid key={ex.nomeExercicio} size={{ xs: 12, sm: 6, md: 4 }}>
                    <Card variant="outlined" sx={{ height: "100%" }}>
                      <CardContent sx={{ pb: "12px !important" }}>
                        <Typography variant="body2" sx={{ fontWeight: 600, mb: 0.5, lineHeight: 1.3 }}>
                          {ex.nomeExercicio}
                        </Typography>
                        {ex.grupoMuscular && (
                          <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 1 }}>
                            {ex.grupoMuscular}
                          </Typography>
                        )}
                        <ProgressaoCargaChart nomeExercicio={ex.nomeExercicio} chartData={chartData} />
                        {ultima && (
                          <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: "block" }}>
                            Último: {ultima.cargaMaxima != null
                              ? `${ultima.cargaMaxima} kg`
                              : `${ultima.seriesExecutadas}×${ultima.repeticoesExecutadas} reps`}
                          </Typography>
                        )}
                      </CardContent>
                    </Card>
                  </Grid>
                );
              })}
            </Grid>
          )}
        </Box>
      )}

      {tab === 4 && (
        <Card variant="outlined">
          <CardContent>
            <Typography variant="subtitle2" sx={{ mb: 2 }}>
              Ações LGPD
            </Typography>
            <Stack spacing={1.5}>
              <Button
                variant="outlined"
                size="small"
                disabled={exportingLgpd || !aluno?.contaId}
                onClick={handleExportarLgpd}
                sx={{ alignSelf: "flex-start" }}
              >
                {exportingLgpd ? "Exportando..." : "Exportar dados (LGPD)"}
              </Button>
              <Button
                variant="outlined"
                color="error"
                size="small"
                disabled={!aluno?.contaId}
                onClick={() => setAnonimizarDialog(true)}
                sx={{ alignSelf: "flex-start" }}
              >
                Anonimizar conta (LGPD)
              </Button>
            </Stack>
          </CardContent>
        </Card>
      )}

      <ConfirmDialog
        open={anonimizarDialog}
        title="Anonimizar conta"
        description="Esta ação é irreversível. Os dados pessoais deste aluno serão anonimizados permanentemente (LGPD - direito ao esquecimento)."
        confirmLabel={anonimizando ? "Anonimizando..." : "Anonimizar"}
        destructive
        loading={anonimizando}
        onConfirm={handleAnonimizar}
        onClose={() => setAnonimizarDialog(false)}
      />
    </Box>
  );
}
