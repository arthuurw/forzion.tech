"use client";
import { Box, Typography, Paper } from "@mui/material";
import { useTheme } from "@mui/material/styles";
import {
  PieChart, Pie, Cell, BarChart, Bar, XAxis, YAxis,
  Tooltip, ResponsiveContainer, Legend,
} from "recharts";
import ChartFigure from "@/components/charts/ChartFigure";

interface StatItem {
  name: string;
  value: number;
  color: string;
}

interface ObjetivoItem {
  name: string;
  total: number;
}

interface ReceitaPacoteItem {
  name: string;
  receita: number;
  alunos: number;
}

export interface TreinadorDashboardChartsProps {
  alunoStats: StatItem[];
  objetivoData: ObjetivoItem[];
  receitaPorPacote: ReceitaPacoteItem[];
}

export default function TreinadorDashboardCharts({
  alunoStats,
  objetivoData,
  receitaPorPacote,
}: TreinadorDashboardChartsProps) {
  const theme = useTheme();

  return (
    <>
      {/* Charts */}
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1fr 1.4fr" }, gap: 2, mb: 4 }}>
        <Paper sx={{ p: 3, borderRadius: 2 }}>
          <Typography
            variant="overline"
            color="text.disabled"
            sx={{ letterSpacing: 2, fontSize: "0.7rem" }}
          >
            ALUNOS POR STATUS
          </Typography>
          <ChartFigure
            label="Alunos por status"
            summary={alunoStats.map((s) => `${s.name}: ${s.value}`).join(", ")}
          >
            <ResponsiveContainer width="100%" height={220}>
              <PieChart>
                <Pie
                  data={alunoStats}
                  cx="50%"
                  cy="50%"
                  innerRadius={55}
                  outerRadius={85}
                  dataKey="value"
                  paddingAngle={3}
                >
                  {alunoStats.map((entry, i) => (
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
          <Typography
            variant="overline"
            color="text.disabled"
            sx={{ letterSpacing: 2, fontSize: "0.7rem" }}
          >
            FICHAS POR OBJETIVO
          </Typography>
          {objetivoData.length === 0 ? (
            <Box sx={{ display: "flex", alignItems: "center", height: 220 }}>
              <Typography variant="body2" color="text.secondary">
                Nenhuma ficha criada ainda.
              </Typography>
            </Box>
          ) : (
            <ChartFigure
              label="Fichas por objetivo"
              summary={objetivoData.map((d) => `${d.name}: ${d.total}`).join(", ")}
            >
              <ResponsiveContainer width="100%" height={220}>
                <BarChart data={objetivoData} layout="vertical" margin={{ left: 8, right: 16 }}>
                  <XAxis type="number" allowDecimals={false} tick={{ fontSize: 11 }} />
                  <YAxis type="category" dataKey="name" width={110} tick={{ fontSize: 12 }} />
                  <Tooltip />
                  <Bar dataKey="total" name="Fichas" fill={theme.palette.primary.main} radius={[0, 4, 4, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </ChartFigure>
          )}
        </Paper>
      </Box>

      {/* Receita por pacote */}
      {receitaPorPacote.length > 0 && (
        <Paper sx={{ p: 3, borderRadius: 2, mb: 4 }}>
          <Typography
            variant="overline"
            color="text.disabled"
            sx={{ letterSpacing: 2, fontSize: "0.7rem" }}
          >
            RECEITA POR PACOTE
          </Typography>
          <ChartFigure
            label="Receita por pacote"
            summary={receitaPorPacote.map((d) => `${d.name}: ${d.receita.toLocaleString("pt-BR", { style: "currency", currency: "BRL" })}`).join(", ")}
          >
          <ResponsiveContainer width="100%" height={Math.max(120, receitaPorPacote.length * 52)}>
            <BarChart data={receitaPorPacote} layout="vertical" margin={{ left: 8, right: 24 }}>
              <XAxis
                type="number"
                tickFormatter={(v: number) => v.toLocaleString("pt-BR", { style: "currency", currency: "BRL" })}
                tick={{ fontSize: 11 }}
              />
              <YAxis type="category" dataKey="name" width={120} tick={{ fontSize: 12 }} />
              <Tooltip
                formatter={(value) => {
                  const v = typeof value === "number" ? value : Number(value);
                  return [v.toLocaleString("pt-BR", { style: "currency", currency: "BRL" }), "Receita"];
                }}
              />
              <Bar dataKey="receita" name="receita" fill={theme.palette.success.main} radius={[0, 4, 4, 0]} />
            </BarChart>
          </ResponsiveContainer>
          </ChartFigure>
        </Paper>
      )}
    </>
  );
}
