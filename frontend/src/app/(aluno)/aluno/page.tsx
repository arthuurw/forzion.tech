"use client";
import { useEffect, useState } from "react";
import {
  Box, Typography, Paper, Stack, Divider, Button,
} from "@mui/material";
import FitnessCenterIcon from "@mui/icons-material/FitnessCenter";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import Link from "next/link";
import {
  PieChart, Pie, Cell, BarChart, Bar, XAxis, YAxis,
  Tooltip, ResponsiveContainer, Legend,
} from "recharts";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import AlertBanner from "@/components/ui/AlertBanner";
import SemVinculoAtivoBanner from "@/components/aluno/SemVinculoAtivoBanner";
import { alunoApi, type TreinoAlunoDetalheResponse } from "@/lib/api/aluno";
import { OBJETIVO_LABEL } from "@/lib/constants/labels";
import { getWeekLabel } from "@/lib/utils/formatting";

export default function DashboardAlunoPage() {
  const [totalFichas, setTotalFichas] = useState(0);
  const [totalExecucoes, setTotalExecucoes] = useState(0);
  const [fichasStats, setFichasStats] = useState<{ name: string; value: number; color: string }[]>([]);
  const [sessoesData, setSessoesData] = useState<{ semana: string; sessoes: number }[]>([]);
  const [fichasAtivas, setFichasAtivas] = useState<TreinoAlunoDetalheResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    const load = async () => {
      try {
        const [fichasRes, execucoesRes] = await Promise.all([
          alunoApi.listFichas({ tamanhoPagina: 50 }),
          alunoApi.listExecucoes({ tamanhoPagina: 100 }),
        ]);

        const fichas = fichasRes.data.items;
        const execucoes = execucoesRes.data.items;

        setTotalFichas(fichasRes.data.total);
        setTotalExecucoes(execucoesRes.data.total);

        const ativas = fichas.filter((f) => f.status === "Ativo");
        setFichasStats([
          { name: "Ativas", value: ativas.length, color: "#4caf50" },
          { name: "Inativas", value: fichas.length - ativas.length, color: "#757575" },
        ]);
        setFichasAtivas(ativas.slice(0, 5));

        const hoje = new Date();
        const weekKeys: string[] = [];
        for (let i = 7; i >= 0; i--) {
          const d = new Date(hoje);
          d.setDate(d.getDate() - i * 7);
          const label = getWeekLabel(d.toISOString());
          if (!weekKeys.includes(label)) weekKeys.push(label);
        }
        const counts: Record<string, number> = {};
        for (const w of weekKeys) counts[w] = 0;
        for (const ex of execucoes) {
          const w = getWeekLabel(ex.dataExecucao);
          if (w in counts) counts[w]++;
        }
        setSessoesData(weekKeys.map((w) => ({ semana: w, sessoes: counts[w] })));
      } catch {
        setError("Erro ao carregar dados.");
      } finally {
        setLoading(false);
      }
    };
    load();
  }, []);

  if (loading) return <LoadingSpinner />;

  return (
    <Box>
      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      <SemVinculoAtivoBanner />

      {/* Stat cards */}
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2, mb: 4 }}>
        <Paper sx={{ p: 3, borderLeft: "4px solid #4caf50", borderRadius: 2 }}>
          <Typography variant="h3" sx={{ fontWeight: 800, lineHeight: 1, color: "#4caf50" }}>
            {totalFichas}
          </Typography>
          <Typography variant="caption" color="text.secondary" sx={{ letterSpacing: 0.5 }}>
            Fichas disponíveis
          </Typography>
        </Paper>
        <Paper sx={{ p: 3, borderLeft: "4px solid #1976d2", borderRadius: 2 }}>
          <Typography variant="h3" sx={{ fontWeight: 800, lineHeight: 1, color: "#1976d2" }}>
            {totalExecucoes}
          </Typography>
          <Typography variant="caption" color="text.secondary" sx={{ letterSpacing: 0.5 }}>
            Sessões realizadas
          </Typography>
        </Paper>
      </Box>

      {/* Charts */}
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1fr 1.4fr" }, gap: 2, mb: 4 }}>
        <Paper sx={{ p: 3, borderRadius: 2 }}>
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
          )}
        </Paper>

        <Paper sx={{ p: 3, borderRadius: 2 }}>
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
            <ResponsiveContainer width="100%" height={220}>
              <BarChart data={sessoesData} margin={{ left: -16, right: 16 }}>
                <XAxis dataKey="semana" tick={{ fontSize: 11 }} />
                <YAxis allowDecimals={false} tick={{ fontSize: 11 }} />
                <Tooltip formatter={(v) => [v, "Sessões"]} />
                <Bar dataKey="sessoes" name="Sessões" fill="#F5C400" radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          )}
        </Paper>
      </Box>

      {/* Fichas ativas */}
      <Paper sx={{ p: 3, borderRadius: 2 }}>
        <Typography
          variant="overline"
          color="text.disabled"
          sx={{ letterSpacing: 2, fontSize: "0.7rem", display: "block", mb: 1 }}
        >
          FICHAS ATIVAS
        </Typography>

        {fichasAtivas.length === 0 ? (
          <Typography variant="body2" color="text.secondary" sx={{ py: 2 }}>
            Nenhuma ficha ativa disponível.
          </Typography>
        ) : (
          <>
            <Stack divider={<Divider />}>
              {fichasAtivas.map((f) => (
                <Box
                  key={f.treinoAlunoId}
                  sx={{
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    gap: 2,
                    py: 2,
                    flexWrap: "wrap",
                  }}
                >
                  <Box>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      {f.nomeTreino}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {OBJETIVO_LABEL[f.objetivo] ?? f.objetivo} · {f.exercicios.length} exercício{f.exercicios.length !== 1 ? "s" : ""}
                    </Typography>
                  </Box>
                  <Stack direction="row" spacing={1} sx={{ flexShrink: 0 }}>
                    <Link href={`/aluno/fichas/${f.treinoAlunoId}`} style={{ textDecoration: "none" }}>
                      <Button size="small" variant="contained" startIcon={<FitnessCenterIcon />}>
                        Ver ficha
                      </Button>
                    </Link>
                    <Link href={`/aluno/fichas/${f.treinoAlunoId}/executar`} style={{ textDecoration: "none" }}>
                      <Button size="small" variant="outlined" startIcon={<PlayArrowIcon />}>
                        Executar
                      </Button>
                    </Link>
                  </Stack>
                </Box>
              ))}
            </Stack>
            {totalFichas > 5 && (
              <Box sx={{ pt: 2 }}>
                <Link href="/aluno/fichas" style={{ textDecoration: "none" }}>
                  <Button size="small" variant="text">
                    Ver todas ({totalFichas})
                  </Button>
                </Link>
              </Box>
            )}
          </>
        )}
      </Paper>
    </Box>
  );
}
