"use client";
import {
  LineChart, Line, XAxis, YAxis, CartesianGrid,
  Tooltip as ChartTooltip, ResponsiveContainer,
} from "recharts";
import ChartFigure from "@/components/charts/ChartFigure";

interface CargaPoint { data: string; carga: number | null }

export interface ProgressaoCargaChartProps {
  nomeExercicio: string;
  chartData: CargaPoint[];
}

export default function ProgressaoCargaChart({ nomeExercicio, chartData }: ProgressaoCargaChartProps) {
  return (
    <ChartFigure
      label={`Progressão de carga — ${nomeExercicio}`}
      summary={chartData.map((d) => `${d.data}: ${d.carga ?? "—"} kg`).join(", ")}
    >
      <ResponsiveContainer width="100%" height={140}>
        <LineChart data={chartData} margin={{ top: 4, right: 8, bottom: 0, left: -20 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="#e0e0e0" />
          <XAxis dataKey="data" tick={{ fontSize: 10 }} stroke="#999" />
          <YAxis tick={{ fontSize: 10 }} stroke="#999" />
          <ChartTooltip
            contentStyle={{ background: "#1a1a1a", border: "1px solid #333", borderRadius: 4, fontSize: 11 }}
            labelStyle={{ color: "#888" }}
            itemStyle={{ color: "#F5C400" }}
            formatter={(value) => [`${value} kg`, "Carga"]}
          />
          <Line
            type="monotone"
            dataKey="carga"
            stroke="#F5C400"
            strokeWidth={2}
            dot={{ r: 3, fill: "#F5C400" }}
            activeDot={{ r: 5 }}
            connectNulls
          />
        </LineChart>
      </ResponsiveContainer>
    </ChartFigure>
  );
}
