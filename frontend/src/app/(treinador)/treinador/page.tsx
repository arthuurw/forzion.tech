"use client";
import { useEffect, useMemo, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { queryKeys } from "@/lib/query/keys";
import dynamic from "next/dynamic";
import {
  Box, Typography, Paper, Stack, Divider, Button,
} from "@mui/material";
import { useTheme, alpha } from "@mui/material/styles";
import CheckIcon from "@mui/icons-material/Check";
import LinkOffIcon from "@mui/icons-material/LinkOff";
import { useRouter } from "next/navigation";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import AlertBanner from "@/components/ui/AlertBanner";
import TierEfetivoBadge from "@/components/treinador/TierEfetivoBadge";
import { useTreinadorDashboard } from "@/lib/hooks/useTreinadorDashboard";
import { treinadorApi } from "@/lib/api/treinador";
import { pagamentoApi } from "@/lib/api/pagamento";
import { extractApiError } from "@/lib/api/extractApiError";
import type { VinculoDetalheResponse } from "@/types";
import type { TierEfetivoStatus } from "@/lib/utils/tier";
import { OBJETIVO_LABEL, ALUNO_STATUS_COLORS } from "@/lib/constants/labels";

const TreinadorDashboardCharts = dynamic(
  () => import("./_charts/TreinadorDashboardCharts"),
  { ssr: false, loading: () => <LoadingSpinner /> },
);

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
  const queryClient = useQueryClient();
  const [error, setError] = useState("");
  const [actionLoading, setActionLoading] = useState<string | null>(null);

  const { data: dashboard, isPending, isError, error: queryError } = useTreinadorDashboard();

  const {
    data: planos = [],
    isPending: planosPending,
    isError: planosIsError,
  } = useQuery({
    queryKey: queryKeys.catalog.planosPlataforma,
    staleTime: 60 * 1000,
    queryFn: () => pagamentoApi.listarPlanosPlataforma().then((r) => r.data),
  });

  const planosStatus: TierEfetivoStatus = planosPending ? "loading" : planosIsError ? "error" : "resolved";

  const data = useMemo(() => {
    if (!dashboard) return undefined;

    const alunoStats: StatItem[] = [
      { name: "Ativos", value: dashboard.counts.ativos, color: ALUNO_STATUS_COLORS.Ativos },
      { name: "Aguardando", value: dashboard.counts.aguardando, color: ALUNO_STATUS_COLORS.Aguardando },
      { name: "Inativos", value: dashboard.counts.inativos, color: ALUNO_STATUS_COLORS.Inativos },
    ];

    const receitaPorPacote: ReceitaPacoteItem[] = dashboard.receitaPorPacote.map((p) => ({
      name: p.nome,
      receita: p.receita,
      alunos: p.alunos,
    }));

    const objetivoData: ObjetivoItem[] = dashboard.objetivos
      .slice()
      .sort((a, b) => b.total - a.total)
      .map((o) => ({ name: OBJETIVO_LABEL[o.objetivo] ?? o.objetivo, total: o.total }));

    return {
      alunoStats,
      objetivoData,
      pendentes: dashboard.pendentes,
      totalFichas: dashboard.totalFichas,
      mrr: dashboard.mrr,
      receitaPorPacote,
      onboardingPendente: !dashboard.onboarding.onboardingCompleto,
      modoExterno: dashboard.onboarding.modoPagamentoAluno === "Externo",
      planoInadimplente: dashboard.plano.status === "Inadimplente",
      dadosFiscaisPendentes: dashboard.dadosFiscaisPendentes,
      pacoteNomes: new Map(dashboard.receitaPorPacote.map((p) => [p.pacoteId, p.nome])),
      plano: dashboard.plano,
    };
  }, [dashboard]);

  useEffect(() => {
    if (isError) setError(extractApiError(queryError, "Erro ao carregar dados do painel."));
  }, [isError, queryError]);

  const refresh = () => queryClient.invalidateQueries({ queryKey: queryKeys.treinador.dashboard });

  const handleAprovar = async (vinculo: VinculoDetalheResponse) => {
    if (!vinculo.pacoteId) {
      router.push("/treinador/alunos");
      return;
    }
    setActionLoading(`${vinculo.vinculoId}_aprovar`);
    try {
      await treinadorApi.aprovarVinculo(vinculo.vinculoId, vinculo.pacoteId);
      await refresh();
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
      await refresh();
    } catch (err) {
      setError(extractApiError(err, "Erro ao desvincular aluno."));
    } finally {
      setActionLoading(null);
    }
  };

  if (isPending) return <LoadingSpinner />;

  return (
    <Box>
      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      {data?.plano && (
        <Box sx={{ mb: 2 }}>
          <TierEfetivoBadge plano={data.plano} planos={planos} planosStatus={planosStatus} />
        </Box>
      )}

      {data?.planoInadimplente && (
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

      {data?.dadosFiscaisPendentes && (
        <Paper
          sx={{
            p: 2.5,
            mb: 3,
            borderRadius: 2,
            border: "1px solid",
            borderColor: "warning.main",
            bgcolor: (theme) => alpha(theme.palette.warning.main, 0.1),
          }}
        >
          <Typography variant="body2" sx={{ fontWeight: 700, mb: 0.5 }}>
            Complete seus dados fiscais
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
            Preencha seus dados fiscais para que suas notas fiscais possam ser emitidas automaticamente.
          </Typography>
          <Button variant="contained" color="warning" size="small" onClick={() => router.push("/treinador/dados-fiscais")}>
            Completar dados fiscais
          </Button>
        </Paper>
      )}

      {data?.onboardingPendente && !data?.modoExterno && (
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

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "repeat(2, 1fr)", sm: "repeat(3, 1fr)", md: "repeat(5, 1fr)" }, gap: 2, mb: 4 }}>
        {(data?.alunoStats ?? []).map((s) => (
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
            {data?.totalFichas ?? 0}
          </Typography>
          <Typography variant="caption" color="text.secondary" sx={{ letterSpacing: 0.5 }}>
            Fichas
          </Typography>
        </Paper>
        <Paper sx={{ p: 3, borderLeft: `4px solid ${theme.palette.success.main}`, borderRadius: 2 }}>
          <Typography variant="h4" sx={{ fontWeight: 800, lineHeight: 1.2, color: "success.main" }}>
            {(data?.mrr ?? 0).toLocaleString("pt-BR", { style: "currency", currency: "BRL" })}
          </Typography>
          <Typography variant="caption" color="text.secondary" sx={{ letterSpacing: 0.5 }}>
            Receita Est./mês
          </Typography>
        </Paper>
      </Box>

      <TreinadorDashboardCharts
        alunoStats={data?.alunoStats ?? []}
        objetivoData={data?.objetivoData ?? []}
        receitaPorPacote={data?.receitaPorPacote ?? []}
      />

      <Paper sx={{ p: 3, borderRadius: 2 }}>
        <Typography
          variant="overline"
          color="text.disabled"
          sx={{ letterSpacing: 2, fontSize: "0.7rem", display: "block", mb: 1 }}
        >
          VÍNCULOS AGUARDANDO APROVAÇÃO
        </Typography>

        {(data?.pendentes ?? []).length === 0 ? (
          <Typography variant="body2" color="text.secondary" sx={{ py: 2 }}>
            Nenhum vínculo pendente.
          </Typography>
        ) : (
          <Stack divider={<Divider />}>
            {(data?.pendentes ?? []).map((v) => (
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
                <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap", rowGap: 1 }}>
                  {v.pacoteId && data?.pacoteNomes?.has(v.pacoteId) && (
                    <Typography variant="caption" color="text.secondary">
                      Pacote: <strong>{data.pacoteNomes.get(v.pacoteId)}</strong>
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
