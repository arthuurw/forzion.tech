"use client";
import { Box, Typography, Paper } from "@mui/material";
import { useTheme } from "@mui/material/styles";
import {
  BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer,
} from "recharts";
import ChartFigure from "@/components/charts/ChartFigure";
import { getWeekLabel } from "@/lib/utils/formatting";
import type { SessaoSemanaItem } from "@/types";

export interface AlunoDashboardChartsProps {
  sessoesPorSemana: SessaoSemanaItem[];
}

export default function AlunoDashboardCharts({ sessoesPorSemana }: AlunoDashboardChartsProps) {
  const theme = useTheme();

  const sessoesData = sessoesPorSemana.map((s) => ({
    semana: getWeekLabel(s.semanaInicio),
    sessoes: Number(s.total),
  }));

  const isEmpty = sessoesData.every((s) => s.sessoes === 0);

  return (
    <Paper sx={{ p: { xs: 2, md: 3 }, borderRadius: 2, mb: 4 }}>
      <Typography variant="overline" color="text.disabled" sx={{ letterSpacing: 2, fontSize: "0.7rem" }}>
        SESSÕES POR SEMANA
      </Typography>
      {isEmpty ? (
        <Box sx={{ display: "flex", alignItems: "center", height: 220 }}>
          <Typography variant="body2" color="text.secondary">
            Nenhuma sessão registrada nas últimas 8 semanas.
          </Typography>
        </Box>
      ) : (
        <ChartFigure
          label="Sessões por semana"
          summary={sessoesData.map((d) => `${d.semana}: ${d.sessoes}`).join(", ")}
        >
          <ResponsiveContainer width="100%" height={220}>
            <BarChart data={sessoesData} margin={{ left: -16, right: 16 }}>
              <XAxis dataKey="semana" tick={{ fontSize: 11 }} />
              <YAxis allowDecimals={false} tick={{ fontSize: 11 }} />
              <Tooltip formatter={(v) => [v, "Sessões"]} />
              <Bar dataKey="sessoes" name="Sessões" fill={theme.palette.primary.main} radius={[4, 4, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </ChartFigure>
      )}
    </Paper>
  );
}
