"use client";
import { useEffect, useState, useCallback } from "react";
import {
  Box, Typography, Paper, Stack, Divider, Button, Chip,
} from "@mui/material";
import CheckIcon from "@mui/icons-material/Check";
import CloseIcon from "@mui/icons-material/Close";
import {
  PieChart, Pie, Cell, BarChart, Bar, XAxis, YAxis,
  Tooltip, ResponsiveContainer, Legend,
} from "recharts";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import AlertBanner from "@/components/ui/AlertBanner";
import { adminApi } from "@/lib/api/admin";
import type { TreinadorResponse, PlanoTreinadorResponse } from "@/types";

const STATUS_COLORS: Record<string, string> = {
  Ativos: "#4caf50",
  Pendentes: "#F5C400",
  Inativos: "#757575",
};

interface StatItem {
  name: string;
  value: number;
  color: string;
}

interface PlanoItem {
  name: string;
  total: number;
}

export default function DashboardAdminPage() {
  const [stats, setStats] = useState<StatItem[]>([]);
  const [planoData, setPlanoData] = useState<PlanoItem[]>([]);
  const [pendentes, setPendentes] = useState<TreinadorResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [actionLoading, setActionLoading] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const [ativoRes, aguardandoRes, inativoRes, todosRes, planosRes] = await Promise.all([
        adminApi.listTreinadores({ status: "Ativo", tamanhoPagina: 1 }),
        adminApi.listTreinadores({ status: "AguardandoAprovacao", tamanhoPagina: 10 }),
        adminApi.listTreinadores({ status: "Inativo", tamanhoPagina: 1 }),
        adminApi.listTreinadores({ tamanhoPagina: 100 }),
        adminApi.listPlanos(),
      ]);

      setStats([
        { name: "Ativos", value: ativoRes.data.total, color: STATUS_COLORS.Ativos },
        { name: "Pendentes", value: aguardandoRes.data.total, color: STATUS_COLORS.Pendentes },
        { name: "Inativos", value: inativoRes.data.total, color: STATUS_COLORS.Inativos },
      ]);

      setPendentes(aguardandoRes.data.items);

      const planos = planosRes.data as PlanoTreinadorResponse[];
      const planoMap = new Map(planos.map((p) => [p.planoId, p.nome]));
      const contagem: Record<string, number> = {};
      for (const t of todosRes.data.items) {
        const nome = t.planoTreinadorId
          ? (planoMap.get(t.planoTreinadorId) ?? "Plano desconhecido")
          : "Sem plano";
        contagem[nome] = (contagem[nome] ?? 0) + 1;
      }
      setPlanoData(Object.entries(contagem).map(([name, total]) => ({ name, total })));
    } catch {
      setError("Erro ao carregar dados do painel.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const handleAprovar = async (id: string) => {
    setActionLoading(`${id}_aprovar`);
    try {
      await adminApi.aprovarTreinador(id);
      await load();
    } catch {
      setError("Erro ao aprovar treinador.");
    } finally {
      setActionLoading(null);
    }
  };

  const handleReprovar = async (id: string) => {
    setActionLoading(`${id}_reprovar`);
    try {
      await adminApi.reprovarTreinador(id);
      await load();
    } catch {
      setError("Erro ao reprovar treinador.");
    } finally {
      setActionLoading(null);
    }
  };

  if (loading) return <LoadingSpinner />;

  return (
    <Box>
      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      {/* Stat cards */}
      <Box sx={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 2, mb: 4 }}>
        {stats.map((s) => (
          <Paper
            key={s.name}
            sx={{ p: 3, borderLeft: `4px solid ${s.color}`, borderRadius: 2 }}
          >
            <Typography
              variant="h3"
              sx={{ fontWeight: 800, lineHeight: 1, color: s.color }}
            >
              {s.value}
            </Typography>
            <Typography variant="caption" color="text.secondary" sx={{ letterSpacing: 0.5 }}>
              {s.name}
            </Typography>
          </Paper>
        ))}
      </Box>

      {/* Charts */}
      <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1.4fr", gap: 2, mb: 4 }}>
        <Paper sx={{ p: 3, borderRadius: 2 }}>
          <Typography
            variant="overline"
            color="text.disabled"
            sx={{ letterSpacing: 2, fontSize: "0.7rem" }}
          >
            STATUS DOS TREINADORES
          </Typography>
          <ResponsiveContainer width="100%" height={220}>
            <PieChart>
              <Pie
                data={stats}
                cx="50%"
                cy="50%"
                innerRadius={55}
                outerRadius={85}
                dataKey="value"
                paddingAngle={3}
              >
                {stats.map((entry, i) => (
                  <Cell key={i} fill={entry.color} />
                ))}
              </Pie>
              <Tooltip formatter={(v, n) => [v, n]} />
              <Legend iconType="circle" iconSize={10} />
            </PieChart>
          </ResponsiveContainer>
        </Paper>

        <Paper sx={{ p: 3, borderRadius: 2 }}>
          <Typography
            variant="overline"
            color="text.disabled"
            sx={{ letterSpacing: 2, fontSize: "0.7rem" }}
          >
            TREINADORES POR PLANO
          </Typography>
          <ResponsiveContainer width="100%" height={220}>
            <BarChart data={planoData} layout="vertical" margin={{ left: 8, right: 16 }}>
              <XAxis type="number" allowDecimals={false} tick={{ fontSize: 11 }} />
              <YAxis type="category" dataKey="name" width={110} tick={{ fontSize: 12 }} />
              <Tooltip />
              <Bar dataKey="total" name="Treinadores" fill="#F5C400" radius={[0, 4, 4, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </Paper>
      </Box>

      {/* Pending list */}
      <Paper sx={{ p: 3, borderRadius: 2 }}>
        <Typography
          variant="overline"
          color="text.disabled"
          sx={{ letterSpacing: 2, fontSize: "0.7rem", display: "block", mb: 1 }}
        >
          AGUARDANDO REVISÃO
        </Typography>

        {pendentes.length === 0 ? (
          <Typography variant="body2" color="text.secondary" sx={{ py: 2 }}>
            Nenhum cadastro pendente.
          </Typography>
        ) : (
          <Stack divider={<Divider />}>
            {pendentes.map((t) => (
              <Box
                key={t.treinadorId}
                sx={{
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between",
                  py: 2,
                }}
              >
                <Box>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>
                    {t.nome}
                  </Typography>
                  <Chip
                    label="Aguardando aprovação"
                    size="small"
                    sx={{
                      mt: 0.5,
                      fontSize: "0.65rem",
                      bgcolor: "#F5C40020",
                      color: "primary.main",
                      fontWeight: 600,
                    }}
                  />
                </Box>
                <Stack direction="row" spacing={1}>
                  <Button
                    size="small"
                    variant="contained"
                    color="success"
                    startIcon={<CheckIcon />}
                    disabled={!!actionLoading}
                    onClick={() => handleAprovar(t.treinadorId)}
                  >
                    Aprovar
                  </Button>
                  <Button
                    size="small"
                    variant="outlined"
                    color="error"
                    startIcon={<CloseIcon />}
                    disabled={!!actionLoading}
                    onClick={() => handleReprovar(t.treinadorId)}
                  >
                    Reprovar
                  </Button>
                </Stack>
              </Box>
            ))}
          </Stack>
        )}
      </Paper>
    </Box>
  );
}
