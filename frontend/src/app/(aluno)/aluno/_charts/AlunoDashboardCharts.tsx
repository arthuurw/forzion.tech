"use client";
import { Box, Typography, Paper } from "@mui/material";
import { useTheme } from "@mui/material/styles";
import {
  PieChart, Pie, Cell, BarChart, Bar, XAxis, YAxis,
  Tooltip, ResponsiveContainer, Legend,
} from "recharts";
import ChartFigure from "@/components/charts/ChartFigure";

interface FichaStatItem { name: string; value: number; color: string }
interface SessaoItem { semana: string; sessoes: number }

export interface AlunoDashboardChartsProps {
  totalFichas: number;
  fichasStats: FichaStatItem[];
  sessoesData: SessaoItem[];
}

export default function AlunoDashboardCharts({
  totalFichas,
  fichasStats,
  sessoesData,
}: AlunoDashboardChartsProps) {
  const theme = useTheme();

  return (
    <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1fr 1.4fr" }, gap: 2, mb: 4 }}>
      <Paper sx={{ p: { xs: 2, md: 3 }, borderRadius: 2 }}>
        <Typography variant="overline" color="text.disabled" sx={{ letterSpacing: 2, fontSize: "0.7rem" }}>
          FICHAS POR STATUS
        </Typography>
        {totalFichas === 0 ? (
          <Box sx={{ display: "flex", alignItems: "center", height: 220 }}>
            <Typography variant="body2" color="text.secondary">
              Nenhuma ficha vinculada ainda.
            </Typography>
          </Box>
        ) : (
          <ChartFigure
            label="Fichas por status"
            summary={fichasStats.map((s) => `${s.name}: ${s.value}`).join(", ")}
          >
            <ResponsiveContainer width="100%" height={220}>
              <PieChart>
                <Pie
                  data={fichasStats}
                  cx="50%"
                  cy="50%"
                  innerRadius={55}
                  outerRadius={85}
                  dataKey="value"
                  paddingAngle={3}
                >
                  {fichasStats.map((entry, i) => (
                    <Cell key={i} fill={entry.color} />
                  ))}
                </Pie>
                <Tooltip formatter={(v, n) => [v, n]} />
                <Legend iconType="circle" iconSize={10} />
              </PieChart>
            </ResponsiveContainer>
          </ChartFigure>
        )}
      </Paper>

      <Paper sx={{ p: { xs: 2, md: 3 }, borderRadius: 2 }}>
        <Typography variant="overline" color="text.disabled" sx={{ letterSpacing: 2, fontSize: "0.7rem" }}>
          SESSÕES POR SEMANA
        </Typography>
        {sessoesData.every((s) => s.sessoes === 0) ? (
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
    </Box>
  );
}
