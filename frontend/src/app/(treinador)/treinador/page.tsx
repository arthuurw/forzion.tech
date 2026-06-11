"use client";
import { useEffect, useState, useCallback } from "react";
import {
  Box, Typography, Paper, Stack, Divider, Button,
} from "@mui/material";
import { useTheme, alpha } from "@mui/material/styles";
import CheckIcon from "@mui/icons-material/Check";
import LinkOffIcon from "@mui/icons-material/LinkOff";
import { useRouter } from "next/navigation";
import { pagamentoApi } from "@/lib/api/pagamento";
import {
  PieChart, Pie, Cell, BarChart, Bar, XAxis, YAxis,
  Tooltip, ResponsiveContainer, Legend,
} from "recharts";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import AlertBanner from "@/components/ui/AlertBanner";
import { treinadorApi } from "@/lib/api/treinador";
import { extractApiError } from "@/lib/api/extractApiError";
import type { VinculoDetalheResponse, PacoteResponse, TreinoResponse } from "@/types";
import { OBJETIVO_LABEL, ALUNO_STATUS_COLORS } from "@/lib/constants/labels";

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

export default function DashboardTreinadorPage() {
  const theme = useTheme();
  const router = useRouter();
  const [alunoStats, setAlunoStats] = useState<StatItem[]>([]);
  const [objetivoData, setObjetivoData] = useState<ObjetivoItem[]>([]);
  const [pendentes, setPendentes] = useState<VinculoDetalheResponse[]>([]);
  const [pacotes, setPacotes] = useState<PacoteResponse[]>([]);
  const [totalFichas, setTotalFichas] = useState(0);
  const [mrr, setMrr] = useState(0);
  const [receitaPorPacote, setReceitaPorPacote] = useState<ReceitaPacoteItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [onboardingPendente, setOnboardingPendente] = useState(false);
  const [modoExterno, setModoExterno] = useState(false);
  const [planoInadimplente, setPlanoInadimplente] = useState(false);

  const load = useCallback(async () => {
    try {
      const [ativoRes, aguardandoRes, inativoRes, fichasRes, pacotesRes] = await Promise.all([
        treinadorApi.listVinculos({ status: "Ativo", tamanhoPagina: 100 }),
        treinadorApi.listVinculos({ status: "AguardandoAprovacao", tamanhoPagina: 10 }),
        treinadorApi.listVinculos({ status: "Inativo", tamanhoPagina: 1 }),
        treinadorApi.listFichas({ tamanhoPagina: 100 }),
        treinadorApi.listPacotes(),
      ]);

      const pacotesList = pacotesRes.data as PacoteResponse[];
      setPacotes(pacotesList);

      setAlunoStats([
        { name: "Ativos", value: ativoRes.data.total, color: ALUNO_STATUS_COLORS.Ativos },
        { name: "Aguardando", value: aguardandoRes.data.total, color: ALUNO_STATUS_COLORS.Aguardando },
        { name: "Inativos", value: inativoRes.data.total, color: ALUNO_STATUS_COLORS.Inativos },
      ]);

      setPendentes(aguardandoRes.data.items);
      setTotalFichas(fichasRes.data.total);

      // Compute MRR from active vinculos × pacote price
      const precoMap = new Map(pacotesList.map((p) => [p.pacoteId, p]));
      let totalMrr = 0;
      const receitaMap: Record<string, ReceitaPacoteItem> = {};
      for (const v of ativoRes.data.items as VinculoDetalheResponse[]) {
        if (!v.pacoteId) continue;
        const pacote = precoMap.get(v.pacoteId);
        if (!pacote) continue;
        totalMrr += pacote.preco;
        if (!receitaMap[v.pacoteId]) {
          receitaMap[v.pacoteId] = { name: pacote.nome, alunos: 0, receita: 0 };
        }
        receitaMap[v.pacoteId].alunos += 1;
        receitaMap[v.pacoteId].receita += pacote.preco;
      }
      setMrr(totalMrr);
      setReceitaPorPacote(
        Object.values(receitaMap).sort((a, b) => b.receita - a.receita)
      );

      const contagem: Record<string, number> = {};
      for (const f of fichasRes.data.items as TreinoResponse[]) {
        const label = OBJETIVO_LABEL[f.objetivo] ?? f.objetivo;
        contagem[label] = (contagem[label] ?? 0) + 1;
      }
      setObjetivoData(
        Object.entries(contagem)
          .sort((a, b) => b[1] - a[1])
          .map(([name, total]) => ({ name, total }))
      );
    } catch (err) {
      setError(extractApiError(err, "Erro ao carregar dados do painel."));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  useEffect(() => {
    pagamentoApi.verificarOnboarding()
      .then((res) => {
        setModoExterno(res.data.modoPagamentoAluno === "Externo");
        setOnboardingPendente(!res.data.onboardingCompleto);
      })
      .catch(() => setOnboardingPendente(false));
  }, []);

  useEffect(() => {
    pagamentoApi.obterAssinaturaTreinador()
      .then((res) => setPlanoInadimplente(res.data.status === "Inadimplente"))
      .catch(() => setPlanoInadimplente(false));
  }, []);

  const handleAprovar = async (vinculo: VinculoDetalheResponse) => {
    if (!vinculo.pacoteId) {
      // Redirect to /treinador/alunos where the trainer can pick a package.
      router.push("/treinador/alunos");
      return;
    }
    setActionLoading(`${vinculo.vinculoId}_aprovar`);
    try {
      await treinadorApi.aprovarVinculo(vinculo.vinculoId, vinculo.pacoteId);
      await load();
    } catch (err) {
      setError(extractApiError(err, "Erro ao aprovar vínculo."));
    } finally {
      setActionLoading(null);
    }
  };

  const handleDesvincular = async (vinculoId: string) => {
    setActionLoading(`${vinculoId}_desvincular`);
    try {
      await treinadorApi.desvincularAluno(vinculoId);
      await load();
    } catch (err) {
      setError(extractApiError(err, "Erro ao desvincular aluno."));
    } finally {
      setActionLoading(null);
    }
  };

  if (loading) return <LoadingSpinner />;

  return (
    <Box>
      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      {planoInadimplente && (
        <Paper
          sx={{
            p: 2.5,
            mb: 3,
            borderRadius: 2,
            border: "1px solid",
            borderColor: "error.main",
            bgcolor: (theme) => alpha(theme.palette.error.main, 0.08),
          }}
        >
          <Typography variant="body2" sx={{ fontWeight: 700, mb: 0.5 }}>
            Assinatura da plataforma em atraso
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
            Regularize o pagamento do seu plano para manter o acesso completo.
          </Typography>
          <Button variant="contained" color="error" size="small" onClick={() => router.push("/treinador/plano")}>
            Regularizar pagamento
          </Button>
        </Paper>
      )}

      {onboardingPendente && !modoExterno && (
        <Paper
          sx={{
            p: 2.5,
            mb: 3,
            borderRadius: 2,
            border: "1px solid",
            borderColor: "primary.main",
            bgcolor: (theme) => alpha(theme.palette.primary.main, 0.1),
          }}
        >
          <Typography variant="body2" sx={{ fontWeight: 700, mb: 0.5 }}>
            Configure seus recebimentos
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
            Conclua a configuração de recebimentos (Stripe) para aceitar alunos e receber pagamentos.
          </Typography>
          <Button variant="contained" color="primary" size="small" onClick={() => router.push("/treinador/pagamentos")}>
            Configurar recebimentos
          </Button>
        </Paper>
      )}

      {/* Stat cards */}
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "repeat(2, 1fr)", sm: "repeat(3, 1fr)", md: "repeat(5, 1fr)" }, gap: 2, mb: 4 }}>
        {alunoStats.map((s) => (
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
        <Paper sx={{ p: 3, borderLeft: `4px solid ${theme.palette.info.main}`, borderRadius: 2 }}>
          <Typography variant="h3" sx={{ fontWeight: 800, lineHeight: 1, color: "info.main" }}>
            {totalFichas}
          </Typography>
          <Typography variant="caption" color="text.secondary" sx={{ letterSpacing: 0.5 }}>
            Fichas
          </Typography>
        </Paper>
        <Paper sx={{ p: 3, borderLeft: `4px solid ${theme.palette.success.main}`, borderRadius: 2 }}>
          <Typography variant="h4" sx={{ fontWeight: 800, lineHeight: 1.2, color: "success.main" }}>
            {mrr.toLocaleString("pt-BR", { style: "currency", currency: "BRL" })}
          </Typography>
          <Typography variant="caption" color="text.secondary" sx={{ letterSpacing: 0.5 }}>
            Receita Est./mês
          </Typography>
        </Paper>
      </Box>

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
            <ResponsiveContainer width="100%" height={220}>
              <BarChart data={objetivoData} layout="vertical" margin={{ left: 8, right: 16 }}>
                <XAxis type="number" allowDecimals={false} tick={{ fontSize: 11 }} />
                <YAxis type="category" dataKey="name" width={110} tick={{ fontSize: 12 }} />
                <Tooltip />
                <Bar dataKey="total" name="Fichas" fill={theme.palette.primary.main} radius={[0, 4, 4, 0]} />
              </BarChart>
            </ResponsiveContainer>
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
        </Paper>
      )}

      {/* Pending vinculos */}
      <Paper sx={{ p: 3, borderRadius: 2 }}>
        <Typography
          variant="overline"
          color="text.disabled"
          sx={{ letterSpacing: 2, fontSize: "0.7rem", display: "block", mb: 1 }}
        >
          VÍNCULOS AGUARDANDO APROVAÇÃO
        </Typography>

        {pendentes.length === 0 ? (
          <Typography variant="body2" color="text.secondary" sx={{ py: 2 }}>
            Nenhum vínculo pendente.
          </Typography>
        ) : (
          <Stack divider={<Divider />}>
            {pendentes.map((v) => (
              <Box
                key={v.vinculoId}
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
                    {v.nomeAluno}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {v.emailAluno ?? "—"}
                  </Typography>
                </Box>
                <Stack direction="row" spacing={1} sx={{ flexShrink: 0, alignItems: "center" }}>
                  {v.pacoteId && pacotes.length > 0 && (
                    <Typography variant="caption" color="text.secondary">
                      Pacote: <strong>{pacotes.find((p) => p.pacoteId === v.pacoteId)?.nome ?? "—"}</strong>
                    </Typography>
                  )}
                  <Button
                    size="small"
                    variant="contained"
                    color="success"
                    startIcon={<CheckIcon />}
                    disabled={!!actionLoading || !v.pacoteId}
                    onClick={() => handleAprovar(v)}
                  >
                    Aprovar
                  </Button>
                  <Button
                    size="small"
                    variant="outlined"
                    color="error"
                    startIcon={<LinkOffIcon />}
                    disabled={!!actionLoading}
                    onClick={() => handleDesvincular(v.vinculoId)}
                  >
                    Rejeitar
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
