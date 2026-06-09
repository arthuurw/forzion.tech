"use client";
import { useEffect, useState, useCallback } from "react";
import {
  Box, Typography, Paper, Stack, Divider, Button, Chip, Tabs, Tab,
  Table, TableHead, TableRow, TableCell, TableBody,
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
import { TREINADOR_STATUS_COLORS, ALUNO_DASHBOARD_STATUS_COLORS } from "@/lib/constants/labels";
import type {
  TreinadorResponse, PlanoPlataformaResponse, AlunoResponse, GrupoMuscularResponse,
} from "@/types";
const srOnly: React.CSSProperties = {
  position: "absolute",
  width: 1,
  height: 1,
  padding: 0,
  margin: -1,
  overflow: "hidden",
  clip: "rect(0,0,0,0)",
  whiteSpace: "nowrap",
  borderWidth: 0,
};

interface StatItem { name: string; value: number; color: string }
interface PlanoStat { planoId: string; name: string; total: number; preco: number; maxAlunos: number }
interface DistItem { name: string; total: number }

export default function DashboardAdminPage() {
  const [treinadorStats, setTreinadorStats] = useState<StatItem[]>([]);
  const [alunoStats, setAlunoStats] = useState<StatItem[]>([]);
  const [pendentes, setPendentes] = useState<TreinadorResponse[]>([]);
  const [alunosPendentes, setAlunosPendentes] = useState<AlunoResponse[]>([]);
  const [recentTreinadores, setRecentTreinadores] = useState<TreinadorResponse[]>([]);
  const [planoStats, setPlanoStats] = useState<PlanoStat[]>([]);
  const [finalidadeData, setFinalidadeData] = useState<DistItem[]>([]);
  const [planos, setPlanos] = useState<PlanoPlataformaResponse[]>([]);
  const [totalExercicios, setTotalExercicios] = useState(0);
  const [totalGrupos, setTotalGrupos] = useState(0);
  const [tab, setTab] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [actionLoading, setActionLoading] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const [
        ativoTRes, aguardandoTRes, inativoTRes,
        ativoARes, aguardandoARes, inativoARes,
        planosRes, exerciciosRes, gruposRes, statsRes,
        recentTRes,
      ] = await Promise.all([
        adminApi.listTreinadores({ status: "Ativo", tamanhoPagina: 1 }),
        adminApi.listTreinadores({ status: "AguardandoAprovacao", tamanhoPagina: 20 }),
        adminApi.listTreinadores({ status: "Inativo", tamanhoPagina: 1 }),
        adminApi.listAlunos({ status: "Ativo", tamanhoPagina: 1 }),
        adminApi.listAlunos({ status: "AguardandoAprovacao", tamanhoPagina: 20 }),
        adminApi.listAlunos({ status: "Inativo", tamanhoPagina: 1 }),
        adminApi.listPlanos(),
        adminApi.listExerciciosGlobais({ tamanhoPagina: 1 }),
        adminApi.listGruposMusculares(),
        adminApi.getDashboardStats(),
        adminApi.listTreinadores({ tamanhoPagina: 5 }),
      ]);

      setTreinadorStats([
        { name: "Ativos", value: ativoTRes.data.total, color: TREINADOR_STATUS_COLORS.Ativos },
        { name: "Pendentes", value: aguardandoTRes.data.total, color: TREINADOR_STATUS_COLORS.Pendentes },
        { name: "Inativos", value: inativoTRes.data.total, color: TREINADOR_STATUS_COLORS.Inativos },
      ]);

      setAlunoStats([
        { name: "Ativos", value: ativoARes.data.total, color: ALUNO_DASHBOARD_STATUS_COLORS.Ativos },
        { name: "Pendentes", value: aguardandoARes.data.total, color: ALUNO_DASHBOARD_STATUS_COLORS.Pendentes },
        { name: "Inativos", value: inativoARes.data.total, color: ALUNO_DASHBOARD_STATUS_COLORS.Inativos },
      ]);

      setPendentes(aguardandoTRes.data.items);
      setAlunosPendentes(aguardandoARes.data.items);

      const sorted = [...recentTRes.data.items].sort(
        (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
      );
      setRecentTreinadores(sorted.slice(0, 5));

      const planosData = planosRes.data as PlanoPlataformaResponse[];
      setPlanos(planosData);
      const planoMap = new Map(planosData.map((p) => [p.planoId, p]));
      const planoBarStats: PlanoStat[] = statsRes.data.planoDistribuicao.map((d) => {
        const plano = planoMap.get(d.tier);
        return {
          planoId: d.tier,
          name: plano ? plano.nome : d.tier,
          total: d.total,
          preco: plano?.preco ?? 0,
          maxAlunos: plano?.maxAlunos ?? 0,
        };
      }).sort((a, b) => b.total - a.total);
      setPlanoStats(planoBarStats);

      setFinalidadeData(
        statsRes.data.alunoFinalidade
          .slice()
          .sort((a, b) => b.total - a.total)
          .map((d) => ({ name: d.finalidade, total: d.total }))
      );

      setTotalExercicios(exerciciosRes.data.total);
      setTotalGrupos((gruposRes.data as GrupoMuscularResponse[]).length);
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

  const handleAprovarAluno = async (id: string) => {
    setActionLoading(`${id}_aprovar`);
    try {
      await adminApi.alterarStatusAluno(id, "Ativo");
      await load();
    } catch {
      setError("Erro ao aprovar aluno.");
    } finally {
      setActionLoading(null);
    }
  };

  const handleInativarAluno = async (id: string) => {
    setActionLoading(`${id}_inativar`);
    try {
      await adminApi.alterarStatusAluno(id, "Inativo");
      await load();
    } catch {
      setError("Erro ao inativar aluno.");
    } finally {
      setActionLoading(null);
    }
  };

  if (loading) return <LoadingSpinner />;

  const planoBarData = planoStats.map(({ name, total }) => ({ name, total }));

  return (
    <Box>
      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      {/* Stat cards — Treinadores */}
      <Typography variant="overline" color="text.disabled" sx={{ letterSpacing: 2, fontSize: "0.7rem" }}>
        TREINADORES
      </Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", md: "repeat(3, 1fr)" }, gap: 2, mt: 1, mb: 3 }}>
        {treinadorStats.map((s) => (
          <Paper key={s.name} sx={{ p: 3, borderLeft: `4px solid ${s.color}`, borderRadius: 2 }}>
            <Typography variant="h3" sx={{ fontWeight: 800, lineHeight: 1, color: s.color }}>{s.value}</Typography>
            <Typography variant="caption" color="text.secondary" sx={{ letterSpacing: 0.5 }}>{s.name}</Typography>
          </Paper>
        ))}
      </Box>

      {/* Stat cards — Alunos */}
      <Typography variant="overline" color="text.disabled" sx={{ letterSpacing: 2, fontSize: "0.7rem" }}>
        ALUNOS
      </Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", md: "repeat(3, 1fr)" }, gap: 2, mt: 1, mb: 4 }}>
        {alunoStats.map((s) => (
          <Paper key={s.name} sx={{ p: 3, borderLeft: `4px solid ${s.color}`, borderRadius: 2 }}>
            <Typography variant="h3" sx={{ fontWeight: 800, lineHeight: 1, color: s.color }}>{s.value}</Typography>
            <Typography variant="caption" color="text.secondary" sx={{ letterSpacing: 0.5 }}>{s.name}</Typography>
          </Paper>
        ))}
      </Box>

      {/* Tabs */}
      <Box sx={{ borderBottom: 1, borderColor: "divider", mb: 3 }}>
        <Tabs value={tab} onChange={(_, v: number) => setTab(v)}>
          <Tab label="Visão Geral" />
          <Tab label={(pendentes.length + alunosPendentes.length) > 0 ? `Aprovações (${pendentes.length + alunosPendentes.length})` : "Aprovações"} />
          <Tab label="Plataforma" />
        </Tabs>
      </Box>

      {/* ── Tab 0: Visão Geral ── */}
      {tab === 0 && (
        <Box>
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1fr 1.4fr" }, gap: 2, mb: 3 }}>
            <Paper sx={{ p: 3, borderRadius: 2 }}>
              <Typography variant="overline" color="text.disabled" sx={{ letterSpacing: 2, fontSize: "0.7rem" }}>
                STATUS DOS TREINADORES
              </Typography>
              <figure aria-label="Distribuição de treinadores por status" style={{ margin: 0 }}>
                <span style={srOnly}>
                  {treinadorStats.map((s) => `${s.name}: ${s.value}`).join(", ")}
                </span>
                <ResponsiveContainer width="100%" height={220}>
                  <PieChart>
                    <Pie data={treinadorStats} cx="50%" cy="50%" innerRadius={55} outerRadius={85} dataKey="value" paddingAngle={3}>
                      {treinadorStats.map((e, i) => <Cell key={i} fill={e.color} />)}
                    </Pie>
                    <Tooltip formatter={(v, n) => [v, n]} />
                    <Legend iconType="circle" iconSize={10} />
                  </PieChart>
                </ResponsiveContainer>
              </figure>
            </Paper>

            <Paper sx={{ p: 3, borderRadius: 2 }}>
              <Typography variant="overline" color="text.disabled" sx={{ letterSpacing: 2, fontSize: "0.7rem" }}>
                TREINADORES POR PLANO
              </Typography>
              <figure aria-label="Distribuição de treinadores por plano" style={{ margin: 0 }}>
                <span style={srOnly}>
                  {planoBarData.map((d) => `${d.name}: ${d.total}`).join(", ")}
                </span>
                <ResponsiveContainer width="100%" height={220}>
                  <BarChart data={planoBarData} layout="vertical" margin={{ left: 8, right: 16 }}>
                    <XAxis type="number" allowDecimals={false} tick={{ fontSize: 11 }} />
                    <YAxis type="category" dataKey="name" width={110} tick={{ fontSize: 12 }} />
                    <Tooltip />
                    <Bar dataKey="total" name="Treinadores" fill="#F5C400" radius={[0, 4, 4, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              </figure>
            </Paper>
          </Box>

          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1fr 1.4fr" }, gap: 2 }}>
            <Paper sx={{ p: 3, borderRadius: 2 }}>
              <Typography variant="overline" color="text.disabled" sx={{ letterSpacing: 2, fontSize: "0.7rem" }}>
                STATUS DOS ALUNOS
              </Typography>
              <figure aria-label="Distribuição de alunos por status" style={{ margin: 0 }}>
                <span style={srOnly}>
                  {alunoStats.map((s) => `${s.name}: ${s.value}`).join(", ")}
                </span>
                <ResponsiveContainer width="100%" height={220}>
                  <PieChart>
                    <Pie data={alunoStats} cx="50%" cy="50%" innerRadius={55} outerRadius={85} dataKey="value" paddingAngle={3}>
                      {alunoStats.map((e, i) => <Cell key={i} fill={e.color} />)}
                    </Pie>
                    <Tooltip formatter={(v, n) => [v, n]} />
                    <Legend iconType="circle" iconSize={10} />
                  </PieChart>
                </ResponsiveContainer>
              </figure>
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
                <figure aria-label="Distribuição de alunos por finalidade" style={{ margin: 0 }}>
                  <span style={srOnly}>
                    {finalidadeData.map((d) => `${d.name}: ${d.total}`).join(", ")}
                  </span>
                  <ResponsiveContainer width="100%" height={220}>
                    <BarChart data={finalidadeData} layout="vertical" margin={{ left: 8, right: 16 }}>
                      <XAxis type="number" allowDecimals={false} tick={{ fontSize: 11 }} />
                      <YAxis type="category" dataKey="name" width={130} tick={{ fontSize: 12 }} />
                      <Tooltip />
                      <Bar dataKey="total" name="Alunos" fill="#2196f3" radius={[0, 4, 4, 0]} />
                    </BarChart>
                  </ResponsiveContainer>
                </figure>
              )}
            </Paper>
          </Box>
        </Box>
      )}

      {/* ── Tab 1: Aprovações ── */}
      {tab === 1 && (
        <>
        <Paper sx={{ p: 3, borderRadius: 2 }}>
          <Typography variant="overline" color="text.disabled" sx={{ letterSpacing: 2, fontSize: "0.7rem", display: "block", mb: 1 }}>
            TREINADORES AGUARDANDO REVISÃO
          </Typography>

          {pendentes.length === 0 ? (
            <Typography variant="body2" color="text.secondary" sx={{ py: 2 }}>
              Nenhum treinador pendente.
            </Typography>
          ) : (
            <Stack divider={<Divider />}>
              {pendentes.map((t) => (
                <Box
                  key={t.treinadorId}
                  sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", py: 2, gap: 2, flexWrap: "wrap" }}
                >
                  <Box>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>{t.nome}</Typography>
                    <Typography variant="caption" color="text.secondary">
                      Cadastrado em {new Date(t.createdAt).toLocaleDateString("pt-BR")}
                    </Typography>
                  </Box>
                  <Stack direction="row" spacing={1}>
                    <Button
                      size="small" variant="contained" color="success" startIcon={<CheckIcon />}
                      disabled={!!actionLoading} onClick={() => handleAprovar(t.treinadorId)}
                    >
                      Aprovar
                    </Button>
                    <Button
                      size="small" variant="outlined" color="error" startIcon={<CloseIcon />}
                      disabled={!!actionLoading} onClick={() => handleReprovar(t.treinadorId)}
                    >
                      Reprovar
                    </Button>
                  </Stack>
                </Box>
              ))}
            </Stack>
          )}
        </Paper>

        <Paper sx={{ p: 3, borderRadius: 2, mt: 2 }}>
          <Typography variant="overline" color="text.disabled" sx={{ letterSpacing: 2, fontSize: "0.7rem", display: "block", mb: 1 }}>
            ALUNOS AGUARDANDO APROVAÇÃO
          </Typography>

          {alunosPendentes.length === 0 ? (
            <Typography variant="body2" color="text.secondary" sx={{ py: 2 }}>
              Nenhum aluno pendente.
            </Typography>
          ) : (
            <Stack divider={<Divider />}>
              {alunosPendentes.map((a) => (
                <Box
                  key={a.alunoId}
                  sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", py: 2, gap: 2, flexWrap: "wrap" }}
                >
                  <Box>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>{a.nome}</Typography>
                    <Typography variant="caption" color="text.secondary">
                      Cadastrado em {new Date(a.createdAt).toLocaleDateString("pt-BR")}
                    </Typography>
                  </Box>
                  <Stack direction="row" spacing={1}>
                    <Button
                      size="small" variant="contained" color="success" startIcon={<CheckIcon />}
                      disabled={!!actionLoading} onClick={() => handleAprovarAluno(a.alunoId)}
                    >
                      Aprovar
                    </Button>
                    <Button
                      size="small" variant="outlined" color="error" startIcon={<CloseIcon />}
                      disabled={!!actionLoading} onClick={() => handleInativarAluno(a.alunoId)}
                    >
                      Reprovar
                    </Button>
                  </Stack>
                </Box>
              ))}
            </Stack>
          )}
        </Paper>
        </>
      )}

      {/* ── Tab 2: Plataforma ── */}
      {tab === 2 && (
        <Box>
          {/* Platform counters */}
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", md: "repeat(3, 1fr)" }, gap: 2, mb: 3 }}>
            <Paper sx={{ p: 3, borderLeft: "4px solid #7c3aed", borderRadius: 2 }}>
              <Typography variant="h3" sx={{ fontWeight: 800, lineHeight: 1, color: "#7c3aed" }}>{planos.length}</Typography>
              <Typography variant="caption" color="text.secondary" sx={{ letterSpacing: 0.5 }}>Planos</Typography>
            </Paper>
            <Paper sx={{ p: 3, borderLeft: "4px solid #0891b2", borderRadius: 2 }}>
              <Typography variant="h3" sx={{ fontWeight: 800, lineHeight: 1, color: "#0891b2" }}>{totalExercicios}</Typography>
              <Typography variant="caption" color="text.secondary" sx={{ letterSpacing: 0.5 }}>Exercícios Globais</Typography>
            </Paper>
            <Paper sx={{ p: 3, borderLeft: "4px solid #dc2626", borderRadius: 2 }}>
              <Typography variant="h3" sx={{ fontWeight: 800, lineHeight: 1, color: "#dc2626" }}>{totalGrupos}</Typography>
              <Typography variant="caption" color="text.secondary" sx={{ letterSpacing: 0.5 }}>Grupos Musculares</Typography>
            </Paper>
          </Box>

          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", lg: "1.5fr 1fr" }, gap: 2 }}>
            {/* Plans table */}
            <Paper sx={{ p: 3, borderRadius: 2 }}>
              <Typography variant="overline" color="text.disabled" sx={{ letterSpacing: 2, fontSize: "0.7rem", display: "block", mb: 2 }}>
                PLANOS DE TREINADORES
              </Typography>
              <Box sx={{ overflowX: "auto" }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 600 }}>Nome</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Preço/mês</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Máx. Alunos</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Treinadores</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {planoStats.filter((p) => p.planoId !== "__none").map((p) => (
                    <TableRow key={p.planoId}>
                      <TableCell>{p.name}</TableCell>
                      <TableCell>{p.preco.toLocaleString("pt-BR", { style: "currency", currency: "BRL" })}</TableCell>
                      <TableCell>{p.maxAlunos}</TableCell>
                      <TableCell><Chip label={p.total} size="small" /></TableCell>
                    </TableRow>
                  ))}
                  {planoStats.some((p) => p.planoId === "__none") && (
                    <TableRow>
                      <TableCell colSpan={3} sx={{ color: "text.secondary", fontStyle: "italic" }}>Sem plano atribuído</TableCell>
                      <TableCell>
                        <Chip
                          label={planoStats.find((p) => p.planoId === "__none")?.total ?? 0}
                          size="small"
                          color="warning"
                        />
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
              </Box>
            </Paper>

            {/* Recent trainers */}
            <Paper sx={{ p: 3, borderRadius: 2 }}>
              <Typography variant="overline" color="text.disabled" sx={{ letterSpacing: 2, fontSize: "0.7rem", display: "block", mb: 2 }}>
                TREINADORES RECENTES
              </Typography>
              {recentTreinadores.length === 0 ? (
                <Typography variant="body2" color="text.secondary">Nenhum treinador cadastrado.</Typography>
              ) : (
                <Stack divider={<Divider />}>
                  {recentTreinadores.map((t) => (
                    <Box key={t.treinadorId} sx={{ py: 1.5 }}>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>{t.nome}</Typography>
                      <Stack direction="row" spacing={1} sx={{ alignItems: "center", mt: 0.5 }}>
                        <Chip
                          label={
                            t.status === "Ativo" ? "Ativo"
                              : t.status === "AguardandoAprovacao" ? "Pendente"
                              : "Inativo"
                          }
                          size="small"
                          sx={{
                            fontSize: "0.65rem",
                            fontWeight: 600,
                            bgcolor:
                              t.status === "Ativo" ? "#4caf5020"
                                : t.status === "AguardandoAprovacao" ? "#F5C40020"
                                : "#75757520",
                            color:
                              t.status === "Ativo" ? TREINADOR_STATUS_COLORS.Ativos
                                : t.status === "AguardandoAprovacao" ? "#b8860b"
                                : TREINADOR_STATUS_COLORS.Inativos,
                          }}
                        />
                        <Typography variant="caption" color="text.secondary">
                          {new Date(t.createdAt).toLocaleDateString("pt-BR")}
                        </Typography>
                      </Stack>
                    </Box>
                  ))}
                </Stack>
              )}
            </Paper>
          </Box>
        </Box>
      )}
    </Box>
  );
}
