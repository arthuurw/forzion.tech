"use client";
import { useEffect, useRef, useState } from "react";
import { Box, Typography, Chip, CircularProgress, Paper, Stack, Alert } from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import { isAxiosError } from "axios";
import { pagamentoApi } from "@/lib/api/pagamento";
import CopiarPixButton from "@/components/pagamento/CopiarPixButton";
import type { PagamentoResponse } from "@/types";

const MAX_CONSECUTIVE_ERRORS = 3;

interface Props {
  pagamentoId: string;
  onPago?: () => void;
}

export default function PagamentoPix({ pagamentoId, onPago }: Props) {
  const [pagamento, setPagamento] = useState<PagamentoResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [pollingError, setPollingError] = useState<"auth" | "network" | null>(null);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const consecutiveErrorsRef = useRef(0);

  // onPago via ref: callers passam callback inline (não memoizado). Mantê-lo nas
  // deps do effect reiniciaria o polling a cada render do pai (fetch imediato +
  // novo interval). A ref deixa o effect depender só de pagamentoId.
  const onPagoRef = useRef(onPago);
  useEffect(() => { onPagoRef.current = onPago; }, [onPago]);

  useEffect(() => {
    let active = true;

    const stopPolling = () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
      intervalRef.current = null;
    };

    const carregar = async () => {
      try {
        const res = await pagamentoApi.obterPagamento(pagamentoId);
        if (!active) return;
        consecutiveErrorsRef.current = 0;
        setPagamento(res.data);
        if (res.data.status === "Pago") {
          stopPolling();
          onPagoRef.current?.();
        }
      } catch (err) {
        if (!active) return;
        if (isAxiosError(err) && err.response?.status === 401) {
          // Sessão expirada — parar polling e alertar usuário
          stopPolling();
          setPollingError("auth");
        } else {
          consecutiveErrorsRef.current += 1;
          if (consecutiveErrorsRef.current >= MAX_CONSECUTIVE_ERRORS) {
            setPollingError("network");
          }
          // erros transitórios abaixo do limiar são silenciados
        }
      } finally {
        if (active) setLoading(false);
      }
    };

    carregar();
    intervalRef.current = setInterval(carregar, 30000);

    return () => {
      active = false;
      if (intervalRef.current) clearInterval(intervalRef.current);
      intervalRef.current = null;
    };
  }, [pagamentoId]);

  if (loading) return <CircularProgress />;

  if (pollingError === "auth") {
    return (
      <Alert severity="error" data-testid="polling-auth-error">
        Sua sessão expirou. Faça login novamente para continuar.
      </Alert>
    );
  }

  if (pollingError === "network" && !pagamento) {
    // Não foi possível obter nenhum dado após N tentativas consecutivas
    return (
      <Alert severity="warning" data-testid="polling-network-warning">
        Não foi possível verificar o status do pagamento. Tente recarregar a página.
      </Alert>
    );
  }

  if (!pagamento) return null;

  if (pagamento.status === "Pago") {
    return (
      <Stack spacing={1} sx={{ alignItems: "center", py: 3 }}>
        <CheckCircleIcon color="success" sx={{ fontSize: 56 }} />
        <Typography variant="h6" color="success.main">Pagamento confirmado!</Typography>
      </Stack>
    );
  }

  if (pagamento.status === "Expirado" || pagamento.status === "Falhou") {
    return (
      <Stack spacing={1} sx={{ alignItems: "center", py: 2 }}>
        <Chip label={pagamento.status === "Expirado" ? "QR expirado" : "Pagamento falhou"} color="error" />
        <Typography variant="body2" color="text.secondary">
          Solicite uma nova cobrança a quem te treina.
        </Typography>
      </Stack>
    );
  }

  const expiracao = pagamento.pixExpiracao ? new Date(pagamento.pixExpiracao) : null;

  return (
    <Paper variant="outlined" sx={{ p: 3, maxWidth: 380, mx: "auto" }}>
      <Stack spacing={2} sx={{ alignItems: "center" }}>
        {pollingError === "network" && (
          <Alert severity="warning" sx={{ width: "100%" }} data-testid="polling-network-warning">
            Não foi possível verificar o status do pagamento. Tente recarregar a página.
          </Alert>
        )}
        <Typography variant="h6">Pague via Pix</Typography>
        <Typography variant="h5" color="primary" sx={{ fontWeight: "bold" }}>
          {pagamento.valor.toLocaleString("pt-BR", { style: "currency", currency: "BRL" })}
        </Typography>

        {pagamento.pixQrCodeUrl && (
          <Box
            component="img"
            src={pagamento.pixQrCodeUrl}
            alt="QR Code Pix"
            sx={{ width: "100%", maxWidth: 220, height: "auto", aspectRatio: "1 / 1" }}
          />
        )}

        {pagamento.pixQrCode && (
          <Box sx={{ width: "100%" }}>
            <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 0.5 }}>
              Pix copia e cola:
            </Typography>
            <Box
              sx={{
                fontSize: 11,
                wordBreak: "break-all",
                bgcolor: "grey.100",
                p: 1,
                borderRadius: 1,
                fontFamily: "monospace",
              }}
            >
              {pagamento.pixQrCode.slice(0, 60)}…
            </Box>
            <CopiarPixButton codigo={pagamento.pixQrCode} />
          </Box>
        )}

        {expiracao && (
          <Typography variant="caption" color="text.secondary">
            Válido até {expiracao.toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" })}
          </Typography>
        )}

        <Chip label="Aguardando pagamento..." size="small" color="warning" />
      </Stack>
    </Paper>
  );
}
