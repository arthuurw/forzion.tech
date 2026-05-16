"use client";
import { useEffect, useState } from "react";
import { Box, Typography, Button, Chip, Stack, CircularProgress, Alert, Paper } from "@mui/material";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import { pagamentoApi } from "@/lib/api/pagamento";
import type { OnboardingStatusResponse } from "@/types";

export default function PagamentosTreinadorPage() {
  const [status, setStatus] = useState<OnboardingStatusResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [iniciando, setIniciando] = useState(false);
  const [error, setError] = useState("");

  const carregar = async () => {
    try {
      const res = await pagamentoApi.verificarOnboarding();
      setStatus(res.data);
    } catch {
      setError("Erro ao verificar status do cadastro Stripe.");
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
    } catch {
      setError("Erro ao iniciar cadastro. Tente novamente.");
      setIniciando(false);
    }
  };

  if (loading) return <Box p={4}><CircularProgress /></Box>;

  return (
    <Box p={4} maxWidth={600}>
      <Typography variant="h5" fontWeight="bold" mb={1}>Recebimentos</Typography>
      <Typography variant="body2" color="text.secondary" mb={3}>
        Configure sua conta Stripe para receber pagamentos dos alunos via Pix.
      </Typography>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <Paper variant="outlined" sx={{ p: 3 }}>
        <Stack spacing={2}>
          <Stack direction="row" alignItems="center" spacing={1}>
            <Typography variant="subtitle1" fontWeight="medium">Status da conta</Typography>
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
        </Stack>
      </Paper>
    </Box>
  );
}
