"use client";
import {
  BarChart, Bar, LineChart, Line,
  XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer,
} from "recharts";
import { useTheme } from "@mui/material/styles";
import ChartFigure from "@/components/charts/ChartFigure";

interface WeekItem { label: string; sessoes: number }
interface CargaPoint { data: string; carga: number | null; series: number; reps: number }

export interface FrequenciaChartProps {
  weekData: WeekItem[];
}

export function FrequenciaChart({ weekData }: FrequenciaChartProps) {
  const theme = useTheme();
  return (
    <ChartFigure
      label="Frequência semanal de sessões"
      summary={weekData.map((d) => `${d.label}: ${d.sessoes}`).join(", ")}
    >
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
    </ChartFigure>
  );
}

export interface ProgressaoCargaChartProps {
  nomeExercicio: string;
  chartData: CargaPoint[];
}

export function ProgressaoCargaChart({ nomeExercicio, chartData }: ProgressaoCargaChartProps) {
  const theme = useTheme();
  return (
    <ChartFigure
      label={`Progressão de carga — ${nomeExercicio}`}
      summary={chartData.map((d) => `${d.data}: ${d.carga} kg`).join(", ")}
    >
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
    </ChartFigure>
  );
}
