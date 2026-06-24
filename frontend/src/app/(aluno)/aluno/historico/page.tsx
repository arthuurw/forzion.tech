"use client";
import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Box, Typography, Card, CardContent, Stack, Chip, Grid, Skeleton,
  ToggleButtonGroup, ToggleButton, useMediaQuery,
} from "@mui/material";
import { useTheme } from "@mui/material/styles";
import {
  BarChart, Bar, LineChart, Line,
  XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer,
} from "recharts";
import AlertBanner from "@/components/ui/AlertBanner";
import SemVinculoAtivoBanner from "@/components/aluno/SemVinculoAtivoBanner";
import DataList from "@/components/ui/DataList";
import DetalheErro from "@/components/ui/DetalheErro";
import type { Column } from "@/components/ui/ResponsiveTable";
import { alunoApi } from "@/lib/api/aluno";
import type { ExecucaoTreinoResponse, ExercicioProgressao } from "@/types";
import { usePaginatedList } from "@/hooks/usePaginatedList";
import { extractApiError } from "@/lib/api/extractApiError";
import { formatarData, periodoParaDatas } from "@/lib/utils/formatting";
import { MAX_PAGE_SIZE } from "@/lib/constants/pagination";
import { srOnly } from "@/lib/utils/a11y";

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

function sessoesPorSemana(execucoes: ExecucaoTreinoResponse[]) {
  const result: { label: string; sessoes: number }[] = [];
  const hoje = new Date();
  for (let i = 7; i >= 0; i--) {
    const fim = new Date(hoje);
    fim.setDate(hoje.getDate() - i * 7);
    fim.setHours(23, 59, 59, 999);
    const ini = new Date(fim);
    ini.setDate(fim.getDate() - 6);
    ini.setHours(0, 0, 0, 0);
    const count = execucoes.filter((ex) => {
      const d = new Date(ex.dataExecucao);
      return d >= ini && d <= fim;
    }).length;
    const label = ini.toLocaleDateString("pt-BR", { day: "2-digit", month: "2-digit" });
    result.push({ label, sessoes: count });
  }
  return result;
}

export default function HistoricoAlunoPage() {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const [allExecucoes, setAllExecucoes] = useState<ExecucaoTreinoResponse[]>([]);
  const [allLoading, setAllLoading] = useState(true);

  const [allError, setAllError] = useState("");

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
    alunoApi.listExecucoes({ pagina: 1, tamanhoPagina: MAX_PAGE_SIZE })
      .then((r) => { if (active) { setAllExecucoes(r.data.items); setAllError(""); } })
      .catch((err) => { if (active) { setAllExecucoes([]); setAllError(extractApiError(err, "Não foi possível carregar os indicadores do histórico.")); } })
      .finally(() => { if (active) setAllLoading(false); });
    return () => { active = false; };
  }, []);

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

  const hoje = new Date();
  const sessoesEsseMes = allExecucoes.filter((ex) => {
    const d = new Date(ex.dataExecucao);
    return d.getMonth() === hoje.getMonth() && d.getFullYear() === hoje.getFullYear();
  }).length;
  const ultimaSessao = allExecucoes.length > 0
    ? new Date(allExecucoes[0].dataExecucao).toLocaleDateString("pt-BR", { day: "2-digit", month: "short" })
    : null;

  const weekData = useMemo(() => sessoesPorSemana(allExecucoes), [allExecucoes]);

  return (
    <Box>
      <Typography variant="h5" sx={{ fontWeight: 700, mb: 3 }}>Histórico de Sessões</Typography>

      <SemVinculoAtivoBanner />

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!allError} message={allError} onClose={() => setAllError("")} />

      {/* Summary chips */}
      <Stack direction="row" spacing={1} sx={{ mb: 3, flexWrap: "wrap", rowGap: 1 }}>
        <Chip label={`${total} sessão${total !== 1 ? "ões" : ""} no total`} variant="outlined" />
        <Chip label={`${sessoesEsseMes} este mês`} variant="outlined" />
        {ultimaSessao && <Chip label={`Última: ${ultimaSessao}`} variant="outlined" />}
      </Stack>

      {/* Frequency bar chart */}
      <Card variant="outlined" sx={{ mb: 3 }}>
        <CardContent>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 2 }}>Frequência semanal</Typography>
          {allLoading ? (
            <Skeleton variant="rectangular" height={160} sx={{ borderRadius: 1 }} />
          ) : (
            <figure aria-label="Frequência semanal de sessões" style={{ margin: 0 }}>
              <span style={srOnly}>
                {weekData.map((d) => `${d.label}: ${d.sessoes}`).join(", ")}
              </span>
              <ResponsiveContainer width="100%" height={160}>
                <BarChart data={weekData} margin={{ top: 4, right: 8, bottom: 0, left: -20 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke={theme.palette.divider} vertical={false} />
                  <XAxis dataKey="label" tick={{ fontSize: 11 }} stroke={theme.palette.text.disabled} />
                  <YAxis tick={{ fontSize: 11 }} stroke={theme.palette.text.disabled} allowDecimals={false} />
                  <Tooltip
                    contentStyle={{ background: theme.palette.secondary.main, border: `1px solid ${theme.palette.secondary.light}`, borderRadius: 4, fontSize: 11 }}
                    labelStyle={{ color: theme.palette.text.disabled }}
                    itemStyle={{ color: theme.palette.primary.main }}
                    formatter={(v) => [v, "Sessões"]}
                  />
                  <Bar dataKey="sessoes" fill={theme.palette.primary.main} radius={[4, 4, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </figure>
          )}
        </CardContent>
      </Card>

      {/* Progression per exercise */}
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
                      <figure aria-label={`Progressão de carga — ${ex.nomeExercicio}`} style={{ margin: 0 }}>
                      <span style={srOnly}>
                        {chartData.map((d) => `${d.data}: ${d.carga} kg`).join(", ")}
                      </span>
                      <ResponsiveContainer width="100%" height={140}>
                        <LineChart data={chartData} margin={{ top: 4, right: 8, bottom: 0, left: -20 }}>
                          <CartesianGrid strokeDasharray="3 3" stroke={theme.palette.divider} />
                          <XAxis dataKey="data" tick={{ fontSize: 10 }} stroke={theme.palette.text.disabled} />
                          <YAxis tick={{ fontSize: 10 }} stroke={theme.palette.text.disabled} />
                          <Tooltip
                            contentStyle={{ background: theme.palette.secondary.main, border: `1px solid ${theme.palette.secondary.light}`, borderRadius: 4, fontSize: 11 }}
                            labelStyle={{ color: theme.palette.text.disabled }}
                            itemStyle={{ color: theme.palette.primary.main }}
                            formatter={(value, name) => {
                              if (name === "carga") return [`${value} kg`, "Carga"];
                              if (name === "series") return [value, "Séries"];
                              if (name === "reps") return [value, "Reps"];
                              return [value, String(name)];
                            }}
                          />
                          <Line
                            type="monotone"
                            dataKey="carga"
                            stroke={theme.palette.primary.main}
                            strokeWidth={2}
                            dot={{ r: 3, fill: theme.palette.primary.main }}
                            activeDot={{ r: 5 }}
                            connectNulls
                          />
                        </LineChart>
                      </ResponsiveContainer>
                      </figure>
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

      {/* Recent sessions table */}
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
