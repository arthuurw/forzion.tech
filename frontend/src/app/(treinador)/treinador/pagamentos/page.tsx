"use client";
import { useEffect, useState } from "react";
import { Box, Typography, Button, Chip, Stack, CircularProgress, Alert, Paper, Divider } from "@mui/material";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import { pagamentoApi } from "@/lib/api/pagamento";
import { extractApiError } from "@/lib/api/extractApiError";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import type { OnboardingStatusResponse } from "@/types";

const COOLDOWN_ACEITE =
  "Confirma a alteração? Um novo ajuste só poderá ser feito depois de 90 dias (3 meses).";

export default function PagamentosTreinadorPage() {
  const [status, setStatus] = useState<OnboardingStatusResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [iniciando, setIniciando] = useState(false);
  const [error, setError] = useState("");
  const [confirmarTroca, setConfirmarTroca] = useState(false);
  const [trocando, setTrocando] = useState(false);

  const carregar = async () => {
    try {
      const res = await pagamentoApi.verificarOnboarding();
      setStatus(res.data);
    } catch (err) {
      setError(extractApiError(err, "Erro ao verificar status do cadastro Stripe."));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { carregar(); }, []);

  const iniciarOnboarding = async () => {
    setIniciando(true);
    setError("");
    try {
      const origin = window.location.origin;
      const res = await pagamentoApi.iniciarOnboarding(
        `${origin}/treinador/onboarding/retorno`,
        `${origin}/treinador/pagamentos`,
      );
      window.location.href = res.data.url;
    } catch (err) {
      setError(extractApiError(err, "Erro ao iniciar cadastro. Tente novamente."));
      setIniciando(false);
    }
  };

  if (loading) return <Box sx={{ p: 4 }}><CircularProgress /></Box>;

  const externo = status?.modoPagamentoAluno === "Externo";
  const liberadoEm = status?.modoPagamentoPodeAlterarEm ? new Date(status.modoPagamentoPodeAlterarEm) : null;
  const cooldownAtivo = !!liberadoEm && liberadoEm > new Date();

  const abrirTroca = () => { setError(""); setConfirmarTroca(true); };

  const alternarModo = async () => {
    setTrocando(true);
    setError("");
    try {
      await pagamentoApi.alterarModoPagamento(externo ? "Plataforma" : "Externo");
      setConfirmarTroca(false);
      await carregar();
    } catch (err) {
      setError(extractApiError(err, "Não foi possível alterar o modo de pagamento. Tente novamente."));
    } finally {
      setTrocando(false);
    }
  };

  const trocaDialog = (
    <ConfirmDialog
      open={confirmarTroca}
      title={externo ? "Voltar a receber pela plataforma" : "Receber por fora da plataforma"}
      description={
        externo
          ? `Será necessário ter a conta Stripe configurada. Assinaturas serão criadas para seus alunos ativos e a cobrança via plataforma recomeça. ${COOLDOWN_ACEITE}`
          : `Suas assinaturas ativas de alunos serão canceladas e você passará a cobrar manualmente, por fora da plataforma. ${COOLDOWN_ACEITE}`
      }
      destructive={!externo}
      confirmLabel={externo ? "Voltar à plataforma" : "Receber por fora"}
      cancelLabel="Voltar"
      loading={trocando}
      onConfirm={alternarModo}
      onClose={() => { if (!trocando) { setConfirmarTroca(false); setError(""); } }}
    >
      {error && <Alert severity="error" sx={{ mt: 2 }}>{error}</Alert>}
    </ConfirmDialog>
  );

  const trocaAcao = (label: string) => (
    <>
      <Divider />
      <Button
        variant="outlined"
        color={externo ? "primary" : "error"}
        onClick={abrirTroca}
        disabled={cooldownAtivo}
        sx={{ alignSelf: "flex-start" }}
      >
        {label}
      </Button>
      {cooldownAtivo && liberadoEm && (
        <Typography variant="caption" color="text.secondary">
          Novo ajuste disponível em {liberadoEm.toLocaleDateString("pt-BR")}
        </Typography>
      )}
    </>
  );

  if (externo) {
    return (
      <Box sx={{ p: { xs: 2, md: 4 }, maxWidth: 600 }}>
        <Typography variant="h5" sx={{ fontWeight: "bold", mb: 1 }}>Recebimentos</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
          Você recebe seus alunos por fora da plataforma.
        </Typography>

        {!confirmarTroca && error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

        <Paper variant="outlined" sx={{ p: { xs: 2, md: 3 } }}>
          <Stack spacing={1.5}>
            <Chip label="Pagamento externo" color="default" size="small" sx={{ alignSelf: "flex-start" }} />
            <Typography variant="body2" color="text.secondary">
              Não há cobrança automática nem cadastro Stripe neste modo. Combine o valor direto com
              cada aluno e gerencie o acesso manualmente: ao desvincular um aluno, ele mantém apenas
              o histórico (somente leitura) até um novo vínculo.
            </Typography>
            {trocaAcao("Voltar a receber pela plataforma")}
          </Stack>
        </Paper>

        {trocaDialog}
      </Box>
    );
  }

  return (
    <Box sx={{ p: { xs: 2, md: 4 }, maxWidth: 600 }}>
      <Typography variant="h5" sx={{ fontWeight: "bold", mb: 1 }}>Recebimentos</Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        Configure sua conta Stripe para receber pagamentos dos alunos via Pix.
      </Typography>

      {!confirmarTroca && error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <Paper variant="outlined" sx={{ p: { xs: 2, md: 3 } }}>
        <Stack spacing={2}>
          <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
            <Typography variant="subtitle1" sx={{ fontWeight: "medium" }}>Status da conta</Typography>
            {status?.onboardingCompleto ? (
              <Chip icon={<CheckCircleIcon />} label="Ativo" color="success" size="small" />
            ) : status?.contaConfigurada ? (
              <Chip label="Cadastro incompleto" color="warning" size="small" />
            ) : (
              <Chip label="Não configurado" color="default" size="small" />
            )}
          </Stack>

          {status?.onboardingCompleto ? (
            <Typography variant="body2" color="text.secondary">
              Sua conta está ativa. Você pode receber pagamentos dos alunos via Pix.
            </Typography>
          ) : (
            <>
              <Typography variant="body2" color="text.secondary">
                {status?.contaConfigurada
                  ? "Seu cadastro na Stripe está incompleto. Clique abaixo para continuar."
                  : "Você ainda não configurou sua conta de recebimentos. O processo leva menos de 5 minutos."}
              </Typography>
              <Button
                variant="contained"
                endIcon={<OpenInNewIcon />}
                onClick={iniciarOnboarding}
                disabled={iniciando}
                sx={{ alignSelf: "flex-start" }}
              >
                {iniciando ? "Redirecionando..." : status?.contaConfigurada ? "Continuar cadastro" : "Configurar recebimentos"}
              </Button>
            </>
          )}

          {trocaAcao("Receber por fora da plataforma")}
        </Stack>
      </Paper>

      {trocaDialog}
    </Box>
  );
}
