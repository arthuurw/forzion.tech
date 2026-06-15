"use client";
import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Box, Typography, Grid, Card, CardContent, Skeleton,
  ToggleButtonGroup, ToggleButton, useMediaQuery, useTheme,
} from "@mui/material";
import {
  LineChart, Line, BarChart, Bar,
  XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer,
} from "recharts";
import { treinadorApi } from "@/lib/api/treinador";
import type { ExercicioProgressao } from "@/types";

import { GRUPO_MUSCULAR_LABEL } from "@/lib/constants/labels";
import { formatarData, periodoParaDatas } from "@/lib/utils/formatting";

// recharts props (stroke/fill/contentStyle) take string literals, not MUI sx — so these
// theme-adjacent hex values live as a local constant instead of theme tokens.
const CHART_COLORS = {
  grid: "#2a2a2a",
  axis: "#555",
  tooltipBg: "#1a1a1a",
  tooltipBorder: "#333",
  tooltipLabel: "#aaa",
  tooltipLabelLine: "#888",
} as const;

type Periodo = "7d" | "30d" | "60d" | "90d" | "6m" | "1a" | "tudo";

const PERIODOS: { value: Periodo; label: string }[] = [
  { value: "7d",   label: "Semana" },
  { value: "30d",  label: "30 dias" },
  { value: "60d",  label: "60 dias" },
  { value: "90d",  label: "90 dias" },
  { value: "6m",   label: "6 meses" },
  { value: "1a",   label: "1 ano" },
  { value: "tudo", label: "Tudo" },
];

interface Props {
  alunoId: string;
}

export default function ProgressaoAluno({ alunoId }: Props) {
  const theme = useTheme();
  const brand = theme.palette.primary.main; // mirrors #F5C400; recharts needs a string, not sx
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));
  const [periodo, setPeriodo] = useState<Periodo>("30d");
  const [exercicios, setExercicios] = useState<ExercicioProgressao[]>([]);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const { de, ate } = periodoParaDatas(periodo);
      const res = await treinadorApi.getProgressaoAluno(alunoId, { de, ate });
      setExercicios(res.data.exercicios);
    } catch {
      setExercicios([]);
    } finally {
      setLoading(false);
    }
  }, [alunoId, periodo]);

  useEffect(() => { load(); }, [load]);

  const grupos = useMemo(() =>
    Array.from(
      exercicios.reduce((map, ex) => {
        const key = ex.grupoMuscular || "Outros";
        if (!map.has(key)) map.set(key, []);
        map.get(key)!.push(ex);
        return map;
      }, new Map<string, ExercicioProgressao[]>())
    ),
    [exercicios]
  );

  const volumePorGrupo = useMemo(() => {
    const map = new Map<string, number>();
    for (const ex of exercicios) {
      const key = ex.grupoMuscular || "Outros";
      const vol = ex.historico.reduce(
        // volume = tonelagem (kg) = séries × reps × carga; sem carga (peso corporal) → 0 kg,
        // não 1, senão poluiria o total em kg com volume que não é kg
        (sum, p) => sum + p.seriesExecutadas * p.repeticoesExecutadas * (p.cargaMaxima ?? 0),
        0
      );
      map.set(key, (map.get(key) ?? 0) + vol);
    }
    return Array.from(map.entries())
      .map(([grupo, volume]) => ({ grupo: GRUPO_MUSCULAR_LABEL[grupo] ?? grupo, volume }))
      .sort((a, b) => b.volume - a.volume);
  }, [exercicios]);

  return (
    <Box sx={{ mt: 4 }}>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 2, flexWrap: "wrap", gap: 1 }}>
        <Typography variant="h6" sx={{ fontWeight: 600 }}>Progressão</Typography>
        <Box sx={{ overflowX: "auto", maxWidth: "100%", pb: 0.5 }}>
          <ToggleButtonGroup
            value={periodo}
            exclusive
            onChange={(_, val) => { if (val) setPeriodo(val); }}
            size="small"
            sx={{ flexWrap: "nowrap" }}
          >
            {PERIODOS.map((p) => (
              <ToggleButton key={p.value} value={p.value} sx={{ fontSize: "0.75rem", px: 1.5, whiteSpace: "nowrap" }}>
                {p.label}
              </ToggleButton>
            ))}
          </ToggleButtonGroup>
        </Box>
      </Box>

      {loading ? (
        <Grid container spacing={2}>
          {[1, 2, 3, 4].map((i) => (
            <Grid key={i} size={{ xs: 12, sm: 6, md: 4 }}>
              <Skeleton variant="rectangular" height={220} sx={{ borderRadius: 1 }} />
            </Grid>
          ))}
        </Grid>
      ) : exercicios.length === 0 ? (
        <Card variant="outlined">
          <CardContent sx={{ textAlign: "center", py: 4 }}>
            <Typography color="text.secondary">
              Nenhuma execução registrada no período selecionado.
            </Typography>
          </CardContent>
        </Card>
      ) : (
        <>
          {volumePorGrupo.length > 0 && (
            <Card variant="outlined" sx={{ mb: 3 }}>
              <CardContent>
                <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5 }}>
                  Volume por grupamento (kg)
                </Typography>
                <ResponsiveContainer width="100%" height={260}>
                  <BarChart
                    data={volumePorGrupo}
                    margin={{ top: 8, right: 16, bottom: isMobile ? 28 : 0, left: 0 }}
                  >
                    <CartesianGrid strokeDasharray="3 3" stroke={CHART_COLORS.grid} vertical={false} />
                    <XAxis
                      dataKey="grupo"
                      type="category"
                      interval={0}
                      tick={{ fontSize: isMobile ? 9 : 11 }}
                      angle={isMobile ? -35 : 0}
                      textAnchor={isMobile ? "end" : "middle"}
                      height={isMobile ? 56 : 30}
                      stroke={CHART_COLORS.axis}
                    />
                    <YAxis type="number" tick={{ fontSize: 10 }} stroke={CHART_COLORS.axis} width={48} />
                    <Tooltip
                      contentStyle={{ background: CHART_COLORS.tooltipBg, border: `1px solid ${CHART_COLORS.tooltipBorder}`, borderRadius: 4, fontSize: 11 }}
                      labelStyle={{ color: CHART_COLORS.tooltipLabel }}
                      itemStyle={{ color: brand }}
                      formatter={(v) => [`${Number(v).toLocaleString("pt-BR")} kg`, "Volume"]}
                    />
                    <Bar dataKey="volume" fill={brand} radius={[4, 4, 0, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              </CardContent>
            </Card>
          )}

          {grupos.map(([grupo, exs]) => (
            <Box key={grupo} sx={{ mb: 3 }}>
              <Typography
                variant="subtitle1"
                sx={{ fontWeight: 700, mb: 1.5, color: "primary.main", textTransform: "uppercase", fontSize: "0.8rem", letterSpacing: 1 }}
              >
                {GRUPO_MUSCULAR_LABEL[grupo] ?? grupo}
              </Typography>
              <Grid container spacing={2}>
                {exs.map((ex) => {
                  const ultima = ex.historico.at(-1);
                  const chartData = ex.historico.map((p) => ({
                    data: formatarData(p.data),
                    carga: p.cargaMaxima,
                    series: p.seriesExecutadas,
                    reps: p.repeticoesExecutadas,
                  }));
                  return (
                    <Grid key={ex.nomeExercicio} size={{ xs: 12, sm: 6, md: 4 }}>
                      <Card variant="outlined" sx={{ height: "100%" }}>
                        <CardContent sx={{ pb: "12px !important" }}>
                          <Typography variant="body2" sx={{ fontWeight: 600, mb: 1, lineHeight: 1.3 }}>
                            {ex.nomeExercicio}
                          </Typography>
                          <ResponsiveContainer width="100%" height={150}>
                            <LineChart data={chartData} margin={{ top: 4, right: 8, bottom: 0, left: -20 }}>
                              <CartesianGrid strokeDasharray="3 3" stroke={CHART_COLORS.grid} />
                              <XAxis dataKey="data" tick={{ fontSize: 10 }} stroke={CHART_COLORS.axis} />
                              <YAxis tick={{ fontSize: 10 }} stroke={CHART_COLORS.axis} />
                              <Tooltip
                                contentStyle={{ background: CHART_COLORS.tooltipBg, border: `1px solid ${CHART_COLORS.tooltipBorder}`, borderRadius: 4, fontSize: 11 }}
                                labelStyle={{ color: CHART_COLORS.tooltipLabelLine }}
                                itemStyle={{ color: brand }}
                                formatter={(value, name) => {
                                  if (name === "carga") return [`${value} kg`, "Carga"];
                                  if (name === "series") return [value, "Séries"];
                                  if (name === "reps") return [value, "Reps"];
                                  return [value, name];
                                }}
                              />
                              <Line
                                type="monotone"
                                dataKey="carga"
                                stroke={brand}
                                strokeWidth={2}
                                dot={{ r: 3, fill: brand }}
                                activeDot={{ r: 5 }}
                                connectNulls
                              />
                            </LineChart>
                          </ResponsiveContainer>
                          {ultima && (
                            <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: "block" }}>
                              Último: {ultima.cargaMaxima != null
                                ? `${ultima.cargaMaxima} kg`
                                : `${ultima.seriesExecutadas}×${ultima.repeticoesExecutadas}`}
                            </Typography>
                          )}
                        </CardContent>
                      </Card>
                    </Grid>
                  );
                })}
              </Grid>
            </Box>
          ))}
        </>
      )}
    </Box>
  );
}
