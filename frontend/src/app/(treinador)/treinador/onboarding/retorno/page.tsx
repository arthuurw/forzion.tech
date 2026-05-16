"use client";
import { useEffect, useState } from "react";
import { Box, Typography, Button, CircularProgress, Stack } from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import ErrorIcon from "@mui/icons-material/Error";
import Link from "next/link";
import { pagamentoApi } from "@/lib/api/pagamento";

export default function OnboardingRetornoPage() {
  const [loading, setLoading] = useState(true);
  const [completo, setCompleto] = useState(false);

  useEffect(() => {
    pagamentoApi.verificarOnboarding()
      .then((res) => setCompleto(res.data.onboardingCompleto))
      .catch(() => setCompleto(false))
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <Box sx={{ p: 4, display: "flex", justifyContent: "center" }}>
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box sx={{ p: 4, maxWidth: 480, mx: "auto", textAlign: "center" }}>
      <Stack spacing={2} sx={{ alignItems: "center" }}>
        {completo ? (
          <>
            <CheckCircleIcon color="success" sx={{ fontSize: 64 }} />
            <Typography variant="h5" sx={{ fontWeight: "bold" }}>Cadastro concluído!</Typography>
            <Typography variant="body2" color="text.secondary">
              Sua conta Stripe está ativa. Você já pode receber pagamentos dos alunos.
            </Typography>
          </>
        ) : (
          <>
            <ErrorIcon color="warning" sx={{ fontSize: 64 }} />
            <Typography variant="h5" sx={{ fontWeight: "bold" }}>Cadastro incompleto</Typography>
            <Typography variant="body2" color="text.secondary">
              Seu cadastro na Stripe ainda não foi concluído. Volte à página de recebimentos para continuar.
            </Typography>
          </>
        )}
        <Button component={Link} href="/treinador/pagamentos" variant="contained" sx={{ mt: 1 }}>
          Ir para recebimentos
        </Button>
      </Stack>
    </Box>
  );
}
