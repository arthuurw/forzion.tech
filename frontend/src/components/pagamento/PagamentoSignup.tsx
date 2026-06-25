"use client";
import { useState } from "react";
import { Elements, PaymentElement, useStripe, useElements } from "@stripe/react-stripe-js";
import { Box, Typography, Button, CircularProgress, Alert, Paper, Stack } from "@mui/material";
import type { IniciarPagamentoPlanoResponse } from "@/types";
import { formatarBRL } from "@/lib/utils/formatting";
import { mapStripeError } from "@/lib/pagamento/stripeErro";
import { getStripe } from "@/lib/pagamento/stripeClient";
import CopiarPixButton from "@/components/pagamento/CopiarPixButton";

function PixView({ pagamento }: { pagamento: IniciarPagamentoPlanoResponse }) {
  const expiracao = pagamento.pixExpiracao ? new Date(pagamento.pixExpiracao) : null;
  return (
    <Paper variant="outlined" sx={{ p: 3, maxWidth: 380, mx: "auto" }}>
      <Stack spacing={2} sx={{ alignItems: "center" }}>
        <Typography variant="h6">Pague via Pix</Typography>
        <Typography variant="h5" color="primary" sx={{ fontWeight: "bold" }}>
          {formatarBRL(pagamento.valor)}
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

        <Alert severity="info" sx={{ width: "100%" }}>
          Assim que confirmarmos o pagamento, enviaremos o e-mail de verificação para liberar seu acesso.
        </Alert>
      </Stack>
    </Paper>
  );
}

function CartaoForm({ valor, onPago }: { valor: number; onPago: () => void }) {
  const stripe = useStripe();
  const elements = useElements();
  const [erro, setErro] = useState("");
  const [processando, setProcessando] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!stripe || !elements) return;

    setProcessando(true);
    setErro("");

    const { error } = await stripe.confirmPayment({
      elements,
      confirmParams: { return_url: window.location.href },
      redirect: "if_required",
    });

    if (error) {
      setErro(mapStripeError(error));
      setProcessando(false);
      return;
    }

    onPago();
  };

  return (
    <Paper variant="outlined" sx={{ p: 3, maxWidth: 420, mx: "auto" }}>
      <Stack spacing={2}>
        <Typography variant="h6">Pagamento com cartão</Typography>
        <Typography variant="h5" color="primary" sx={{ fontWeight: "bold" }}>
          {formatarBRL(valor)}
        </Typography>
        <form onSubmit={handleSubmit}>
          <Stack spacing={2}>
            <PaymentElement />
            {erro && <Alert severity="error">{erro}</Alert>}
            <Button type="submit" variant="contained" disabled={!stripe || !elements || processando} fullWidth>
              {processando ? <CircularProgress size={20} /> : "Pagar"}
            </Button>
          </Stack>
        </form>
      </Stack>
    </Paper>
  );
}

interface Props {
  pagamento: IniciarPagamentoPlanoResponse;
  onPagoCartao: () => void;
}

export default function PagamentoSignup({ pagamento, onPagoCartao }: Props) {
  if (pagamento.metodoPagamento === "Pix") {
    return <PixView pagamento={pagamento} />;
  }

  if (!pagamento.clientSecret) {
    return <Alert severity="error">Dados de pagamento indisponíveis. Tente novamente.</Alert>;
  }

  return (
    <Elements stripe={getStripe()} options={{ clientSecret: pagamento.clientSecret, locale: "pt-BR" }}>
      <CartaoForm valor={pagamento.valor} onPago={onPagoCartao} />
    </Elements>
  );
}
