"use client";
import { Box, Typography, Paper } from "@mui/material";
import { useTheme } from "@mui/material/styles";
import {
  PieChart, Pie, Cell, BarChart, Bar, XAxis, YAxis,
  Tooltip, ResponsiveContainer, Legend,
} from "recharts";
import ChartFigure from "@/components/charts/ChartFigure";

interface StatItem { name: string; value: number; color: string }
interface DistItem { name: string; total: number }

export interface AdminDashboardChartsProps {
  treinadorStats: StatItem[];
  alunoStats: StatItem[];
  planoBarData: DistItem[];
  finalidadeData: DistItem[];
}

export default function AdminDashboardCharts({
  treinadorStats,
  alunoStats,
  planoBarData,
  finalidadeData,
}: AdminDashboardChartsProps) {
  const theme = useTheme();

  return (
    <Box>
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1fr 1.4fr" }, gap: 2, mb: 3 }}>
        <Paper sx={{ p: 3, borderRadius: 2 }}>
          <Typography variant="overline" color="text.disabled" sx={{ letterSpacing: 2, fontSize: "0.7rem" }}>
            STATUS DOS TREINADORES
          </Typography>
          <ChartFigure
            label="Distribuição de treinadores por status"
            summary={treinadorStats.map((s) => `${s.name}: ${s.value}`).join(", ")}
          >
            <ResponsiveContainer width="100%" height={220}>
              <PieChart>
                <Pie data={treinadorStats} cx="50%" cy="50%" innerRadius={55} outerRadius={85} dataKey="value" paddingAngle={3}>
                  {treinadorStats.map((e, i) => <Cell key={i} fill={e.color} />)}
                </Pie>
                <Tooltip formatter={(v, n) => [v, n]} />
                <Legend iconType="circle" iconSize={10} />
              </PieChart>
            </ResponsiveContainer>
          </ChartFigure>
        </Paper>

        <Paper sx={{ p: 3, borderRadius: 2 }}>
          <Typography variant="overline" color="text.disabled" sx={{ letterSpacing: 2, fontSize: "0.7rem" }}>
            TREINADORES POR PLANO
          </Typography>
          <ChartFigure
            label="Distribuição de treinadores por plano"
            summary={planoBarData.map((d) => `${d.name}: ${d.total}`).join(", ")}
          >
            <ResponsiveContainer width="100%" height={220}>
              <BarChart data={planoBarData} layout="vertical" margin={{ left: 8, right: 16 }}>
                <XAxis type="number" allowDecimals={false} tick={{ fontSize: 11 }} />
                <YAxis type="category" dataKey="name" width={110} tick={{ fontSize: 12 }} />
                <Tooltip />
                <Bar dataKey="total" name="Treinadores" fill={theme.palette.primary.main} radius={[0, 4, 4, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </ChartFigure>
        </Paper>
      </Box>

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1fr 1.4fr" }, gap: 2 }}>
        <Paper sx={{ p: 3, borderRadius: 2 }}>
          <Typography variant="overline" color="text.disabled" sx={{ letterSpacing: 2, fontSize: "0.7rem" }}>
            STATUS DOS ALUNOS
          </Typography>
          <ChartFigure
            label="Distribuição de alunos por status"
            summary={alunoStats.map((s) => `${s.name}: ${s.value}`).join(", ")}
          >
            <ResponsiveContainer width="100%" height={220}>
              <PieChart>
                <Pie data={alunoStats} cx="50%" cy="50%" innerRadius={55} outerRadius={85} dataKey="value" paddingAngle={3}>
                  {alunoStats.map((e, i) => <Cell key={i} fill={e.color} />)}
                </Pie>
                <Tooltip formatter={(v, n) => [v, n]} />
                <Legend iconType="circle" iconSize={10} />
              </PieChart>
            </ResponsiveContainer>
          </ChartFigure>
        </Paper>

        <Paper sx={{ p: 3, borderRadius: 2 }}>
          <Typography variant="overline" color="text.disabled" sx={{ letterSpacing: 2, fontSize: "0.7rem" }}>
            ALUNOS POR FINALIDADE
          </Typography>
          {finalidadeData.length === 0 ? (
            <Box sx={{ display: "flex", alignItems: "center", height: 220 }}>
              <Typography variant="body2" color="text.secondary">Nenhum dado disponível.</Typography>
            </Box>
          ) : (
            <ChartFigure
              label="Distribuição de alunos por finalidade"
              summary={finalidadeData.map((d) => `${d.name}: ${d.total}`).join(", ")}
            >
              <ResponsiveContainer width="100%" height={220}>
                <BarChart data={finalidadeData} layout="vertical" margin={{ left: 8, right: 16 }}>
                  <XAxis type="number" allowDecimals={false} tick={{ fontSize: 11 }} />
                  <YAxis type="category" dataKey="name" width={130} tick={{ fontSize: 12 }} />
                  <Tooltip />
                  <Bar dataKey="total" name="Alunos" fill={theme.palette.info.main} radius={[0, 4, 4, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </ChartFigure>
          )}
        </Paper>
      </Box>
    </Box>
  );
}
