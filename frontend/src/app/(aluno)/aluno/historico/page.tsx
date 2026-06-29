"use client";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import dynamic from "next/dynamic";
import {
  Box, Typography, Card, CardContent, Stack, Chip, Grid, Skeleton,
  ToggleButtonGroup, ToggleButton, useMediaQuery,
} from "@mui/material";
import { useTheme } from "@mui/material/styles";
import AlertBanner from "@/components/ui/AlertBanner";
import PageHeader from "@/components/ui/PageHeader";
import SemVinculoAtivoBanner from "@/components/aluno/SemVinculoAtivoBanner";
import DataList from "@/components/ui/DataList";
import DetalheErro from "@/components/ui/DetalheErro";
import type { Column } from "@/components/ui/ResponsiveTable";
import { alunoApi } from "@/lib/api/aluno";
import type { ExecucaoTreinoResponse, ExercicioProgressao } from "@/types";
import { usePaginatedList } from "@/hooks/usePaginatedList";
import { extractApiError } from "@/lib/api/extractApiError";
import { formatarData, getWeekLabel, periodoParaDatas } from "@/lib/utils/formatting";
import { queryKeys } from "@/lib/query/keys";

const FrequenciaChart = dynamic(
  () => import("./_charts/HistoricoCharts").then((m) => m.FrequenciaChart),
  { ssr: false, loading: () => <Skeleton variant="rectangular" height={160} sx={{ borderRadius: 1 }} /> },
);
const ProgressaoCargaChart = dynamic(
  () => import("./_charts/HistoricoCharts").then((m) => m.ProgressaoCargaChart),
  { ssr: false, loading: () => <Skeleton variant="rectangular" height={140} sx={{ borderRadius: 1 }} /> },
);

type Periodo = "7d" | "30d" | "60d" | "90d";

const PERIODOS: { value: Periodo; label: string }[] = [
  { value: "7d",  label: "7 dias"  },
  { value: "30d", label: "30 dias" },
  { value: "60d", label: "60 dias" },
  { value: "90d", label: "90 dias" },
];

const TABLE_COLUMNS: Column[] = [
  { label: "Data" },
  { label: "Treino" },
  { label: "Exercícios" },
  { label: "Observação" },
];

export default function HistoricoAlunoPage() {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));

  const [dashError, setDashError] = useState("");
  const { data: dashboard, isPending: dashLoading, isError: dashIsError, error: dashQueryError } = useQuery({
    queryKey: queryKeys.aluno.dashboard,
    staleTime: 60 * 1000,
    queryFn: () => alunoApi.getDashboard().then((r) => r.data),
  });

  useEffect(() => {
    if (dashIsError) setDashError(extractApiError(dashQueryError, "Não foi possível carregar os indicadores do histórico."));
  }, [dashIsError, dashQueryError]);

  const [periodo, setPeriodo] = useState<Periodo>("30d");
  const [exercicios, setExercicios] = useState<ExercicioProgressao[]>([]);
  const [progLoading, setProgLoading] = useState(true);
  const [progError, setProgError] = useState("");
  const [progReload, setProgReload] = useState(0);

  const fetcher = useCallback(
    (p: number, ps: number) => alunoApi.listExecucoes({ pagina: p + 1, tamanhoPagina: ps }).then((r) => r.data),
    [],
  );
  const { items: execucoes, total, page, pageSize, loading, error, setPage, setPageSize, setError } =
    usePaginatedList<ExecucaoTreinoResponse>({ fetcher, errorMessage: "Erro ao carregar histórico." });

  useEffect(() => {
    let active = true;
    setProgLoading(true);
    const { de, ate } = periodoParaDatas(periodo);
    alunoApi.getMinhaProgressao(de, ate)
      .then((res) => { if (active) { setExercicios(res.data.exercicios); setProgError(""); } })
      .catch((err) => { if (active) { setExercicios([]); setProgError(extractApiError(err, "Não foi possível carregar a progressão.")); } })
      .finally(() => { if (active) setProgLoading(false); });
    return () => { active = false; };
  }, [periodo, progReload]);

  const weekData = useMemo(
    () => (dashboard?.sessoesPorSemana ?? []).map((s) => ({
      label: getWeekLabel(s.semanaInicio),
      sessoes: Number(s.total),
    })),
    [dashboard],
  );

  return (
    <Box>
      <PageHeader title="Histórico de Sessões" />

      <SemVinculoAtivoBanner vinculo={dashboard?.vinculo ?? { ativo: true, pendente: false }} />

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!dashError} message={dashError} onClose={() => setDashError("")} />

      <Stack direction="row" spacing={1} sx={{ mb: 3, flexWrap: "wrap", rowGap: 1 }}>
        <Chip label={`${total} sessão${total !== 1 ? "ões" : ""} no total`} variant="outlined" />
      </Stack>

      <Card variant="outlined" sx={{ mb: 3 }}>
        <CardContent>
          <Typography variant="subtitle2" sx={{ mb: 2 }}>Frequência semanal</Typography>
          {dashLoading ? (
            <Skeleton variant="rectangular" height={160} sx={{ borderRadius: 1 }} />
          ) : (
            <FrequenciaChart weekData={weekData} />
          )}
        </CardContent>
      </Card>

      <Box sx={{ mb: 3 }}>
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
                {isMobile ? p.value : p.label}
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
        ) : progError ? (
          <DetalheErro mensagem={progError} onRetry={() => setProgReload((n) => n + 1)} />
        ) : exercicios.length === 0 ? (
          <Card variant="outlined">
            <CardContent sx={{ textAlign: "center", py: 3 }}>
              <Typography color="text.secondary">Nenhuma execução registrada no período.</Typography>
            </CardContent>
          </Card>
        ) : (
          <Grid container spacing={2}>
            {exercicios.map((ex) => {
              const chartData = ex.historico.map((p) => ({
                data: formatarData(p.data),
                carga: p.cargaMaxima,
                series: p.seriesExecutadas,
                reps: p.repeticoesExecutadas,
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

      <Typography variant="h6" sx={{ fontWeight: 600, mb: 2 }}>Sessões recentes</Typography>
      <DataList
        loading={loading}
        items={execucoes}
        emptyMessage="Nenhuma sessão registrada. Execute uma ficha para iniciar seu histórico."
        columns={TABLE_COLUMNS}
        rowKey={(ex) => ex.execucaoId}
        pagination={{ count: total, page, rowsPerPage: pageSize, onPageChange: setPage, onRowsPerPageChange: setPageSize }}
        renderCell={(ex, i) => {
          if (i === 0) return (
            <>
              <Typography variant="body2" sx={{ fontWeight: 500 }}>
                {new Date(ex.dataExecucao).toLocaleDateString("pt-BR", {
                  weekday: "short", day: "2-digit", month: "short", year: "numeric",
                })}
              </Typography>
              <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                {new Date(ex.createdAt).toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" })}
              </Typography>
            </>
          );
          if (i === 1) return (
            <Typography variant="body2" sx={{ fontWeight: 500 }}>{ex.nomeTreino}</Typography>
          );
          if (i === 2) return (
            <Stack direction="row" spacing={1}>
              <Chip size="small" label={`${ex.totalExercicios} exerc.`} variant="outlined" />
              <Chip size="small" label={`${ex.totalSeries} séries`} variant="outlined" />
            </Stack>
          );
          return <Typography variant="body2" color="text.secondary">{ex.observacao ?? "—"}</Typography>;
        }}
      />
    </Box>
  );
}
