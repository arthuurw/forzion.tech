"use client";
import { useEffect, useRef, useState } from "react";
import { Box, Typography, Chip, CircularProgress, Button, Paper, Stack } from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import { pagamentoApi } from "@/lib/api/pagamento";
import type { PagamentoResponse } from "@/types";

interface Props {
  pagamentoId: string;
  onPago?: () => void;
}

export default function PagamentoPix({ pagamentoId, onPago }: Props) {
  const [pagamento, setPagamento] = useState<PagamentoResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const carregar = async () => {
    try {
      const res = await pagamentoApi.obterPagamento(pagamentoId);
      setPagamento(res.data);
      if (res.data.status === "Pago") {
        clearInterval(intervalRef.current!);
        onPago?.();
      }
    } catch {
      // silencia erros de polling
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    carregar();
    intervalRef.current = setInterval(carregar, 30000);
    return () => clearInterval(intervalRef.current!);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pagamentoId]);

  if (loading) return <CircularProgress />;
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
          Solicite uma nova cobrança ao seu treinador.
        </Typography>
      </Stack>
    );
  }

  const expiracao = pagamento.pixExpiracao ? new Date(pagamento.pixExpiracao) : null;

  return (
    <Paper variant="outlined" sx={{ p: 3, maxWidth: 380, mx: "auto" }}>
      <Stack spacing={2} sx={{ alignItems: "center" }}>
        <Typography variant="h6">Pague via Pix</Typography>
        <Typography variant="h5" color="primary" sx={{ fontWeight: "bold" }}>
          {pagamento.valor.toLocaleString("pt-BR", { style: "currency", currency: "BRL" })}
        </Typography>

        {pagamento.pixQrCodeUrl && (
          <Box
            component="img"
            src={pagamento.pixQrCodeUrl}
            alt="QR Code Pix"
            sx={{ width: 220, height: 220 }}
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
            <Button
              size="small"
              sx={{ mt: 1 }}
              onClick={() => navigator.clipboard.writeText(pagamento.pixQrCode!)}
            >
              Copiar código
            </Button>
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
