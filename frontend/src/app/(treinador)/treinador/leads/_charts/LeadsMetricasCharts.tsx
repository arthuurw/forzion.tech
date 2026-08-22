"use client";
import { Box, Typography, Paper, Grid } from "@mui/material";
import { useTheme } from "@mui/material/styles";
import {
  PieChart, Pie, Cell, BarChart, Bar, XAxis, YAxis,
  Tooltip, ResponsiveContainer, Legend,
} from "recharts";
import ChartFigure from "@/components/charts/ChartFigure";
import { MOTIVO_DESCARTE_LEAD_LABEL } from "@/lib/constants/labels";
import type { ObterMetricasLeadsResponse } from "@/types";

interface StatCardProps {
  label: string;
  value: string | number;
}

function StatCard({ label, value }: StatCardProps) {
  return (
    <Grid size={{ xs: 6, sm: 3 }}>
      <Paper variant="outlined" sx={{ p: 2, height: "100%" }}>
        <Typography variant="overline" color="text.disabled" sx={{ letterSpacing: 1, fontSize: "0.7rem" }}>
          {label}
        </Typography>
        <Typography variant="h4" sx={{ fontWeight: 800 }}>{value}</Typography>
      </Paper>
    </Grid>
  );
}

export interface LeadsMetricasChartsProps {
  metricas: ObterMetricasLeadsResponse;
}

export default function LeadsMetricasCharts({ metricas }: LeadsMetricasChartsProps) {
  const theme = useTheme();

  const origemData = [
    { name: "Agente", value: metricas.porOrigemAgente, color: theme.palette.primary.main },
    { name: "Manual", value: metricas.porOrigemManual, color: theme.palette.secondary.main },
  ];

  const motivoData = Object.entries(metricas.porMotivoDescarte)
    .filter(([, total]) => total > 0)
    .map(([motivo, total]) => ({
      name: MOTIVO_DESCARTE_LEAD_LABEL[motivo as keyof typeof MOTIVO_DESCARTE_LEAD_LABEL] ?? motivo,
      total,
    }));

  const taxaConversaoLabel = `${(metricas.taxaConversao * 100).toFixed(1)}%`;

  return (
    <Box sx={{ mb: 4 }}>
      <Grid container spacing={2} sx={{ mb: 2 }}>
        <StatCard label="Total de leads" value={metricas.total} />
        <StatCard label="Pelo agente" value={metricas.porOrigemAgente} />
        <StatCard label="Convertidos" value={metricas.convertidos} />
        <StatCard label="Taxa de conversão" value={taxaConversaoLabel} />
      </Grid>

      {metricas.total > 0 && (
        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1fr 1.4fr" }, gap: 2 }}>
          <Paper sx={{ p: 3, borderRadius: 2 }}>
            <Typography variant="overline" color="text.disabled" sx={{ letterSpacing: 2, fontSize: "0.7rem" }}>
              LEADS POR ORIGEM
            </Typography>
            <ChartFigure
              label="Leads por origem"
              summary={origemData.map((s) => `${s.name}: ${s.value}`).join(", ")}
            >
              <ResponsiveContainer width="100%" height={220}>
                <PieChart>
                  <Pie
                    data={origemData}
                    cx="50%"
                    cy="50%"
                    innerRadius={55}
                    outerRadius={85}
                    dataKey="value"
                    paddingAngle={3}
                  >
                    {origemData.map((entry, i) => (
                      <Cell key={i} fill={entry.color} />
                    ))}
                  </Pie>
                  <Tooltip formatter={(v, n) => [v, n]} />
                  <Legend iconType="circle" iconSize={10} />
                </PieChart>
              </ResponsiveContainer>
            </ChartFigure>
          </Paper>

          <Paper sx={{ p: 3, borderRadius: 2 }}>
            <Typography variant="overline" color="text.disabled" sx={{ letterSpacing: 2, fontSize: "0.7rem" }}>
              MOTIVOS DE DESCARTE
            </Typography>
            {motivoData.length === 0 ? (
              <Box sx={{ display: "flex", alignItems: "center", height: 220 }}>
                <Typography variant="body2" color="text.secondary">
                  Nenhum lead descartado no período.
                </Typography>
              </Box>
            ) : (
              <ChartFigure
                label="Motivos de descarte"
                summary={motivoData.map((d) => `${d.name}: ${d.total}`).join(", ")}
              >
                <ResponsiveContainer width="100%" height={220}>
                  <BarChart data={motivoData} layout="vertical" margin={{ left: 8, right: 16 }}>
                    <XAxis type="number" allowDecimals={false} tick={{ fontSize: 11 }} />
                    <YAxis type="category" dataKey="name" width={110} tick={{ fontSize: 12 }} />
                    <Tooltip />
                    <Bar dataKey="total" name="Leads" fill={theme.palette.error.main} radius={[0, 4, 4, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              </ChartFigure>
            )}
          </Paper>
        </Box>
      )}
    </Box>
  );
}
