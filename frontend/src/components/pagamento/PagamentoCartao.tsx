"use client";
import { useEffect, useState, useCallback } from "react";
import { loadStripe } from "@stripe/stripe-js";
import { Elements, PaymentElement, useStripe, useElements } from "@stripe/react-stripe-js";
import {
  Box, Typography, Button, CircularProgress, Alert, Paper, Stack,
} from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import { pagamentoApi } from "@/lib/api/pagamento";
import type { PagamentoResponse } from "@/types";

const stripePromise = loadStripe(process.env.NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY!);

// ── Inner form (must be inside <Elements>) ────────────────────────────────────

interface FormProps {
  pagamento: PagamentoResponse;
  onPago?: () => void;
}

function CartaoForm({ pagamento, onPago }: FormProps) {
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
      setErro(error.message ?? "Erro ao processar pagamento.");
      setProcessando(false);
      return;
    }

    // Sem redirect — pagamento confirmado sincronamente (ex: 3DS não necessário)
    onPago?.();
  };

  if (pagamento.status === "Pago") {
    return (
      <Stack alignItems="center" spacing={1} py={3}>
        <CheckCircleIcon color="success" sx={{ fontSize: 56 }} />
        <Typography variant="h6" color="success.main">Pagamento confirmado!</Typography>
      </Stack>
    );
  }

  return (
    <Paper variant="outlined" sx={{ p: 3, maxWidth: 420, mx: "auto" }}>
      <Stack spacing={2}>
        <Typography variant="h6">Pagamento com cartão</Typography>
        <Typography variant="h5" color="primary" fontWeight="bold">
          {pagamento.valor.toLocaleString("pt-BR", { style: "currency", currency: "BRL" })}
        </Typography>

        <form onSubmit={handleSubmit}>
          <Stack spacing={2}>
            <PaymentElement />
            {erro && <Alert severity="error">{erro}</Alert>}
            <Button
              type="submit"
              variant="contained"
              disabled={!stripe || !elements || processando}
              fullWidth
            >
              {processando ? <CircularProgress size={20} /> : "Pagar"}
            </Button>
          </Stack>
        </form>
      </Stack>
    </Paper>
  );
}

// ── Public component ──────────────────────────────────────────────────────────

interface Props {
  pagamentoId: string;
  onPago?: () => void;
}

export default function PagamentoCartao({ pagamentoId, onPago }: Props) {
  const [pagamento, setPagamento] = useState<PagamentoResponse | null>(null);
  const [loading, setLoading] = useState(true);

  const carregar = useCallback(async () => {
    try {
      const res = await pagamentoApi.obterPagamento(pagamentoId);
      setPagamento(res.data);
    } catch {
      // mantém null — erro exibido abaixo
    } finally {
      setLoading(false);
    }
  }, [pagamentoId]);

  useEffect(() => { carregar(); }, [carregar]);

  if (loading) return <CircularProgress />;

  if (!pagamento?.clientSecret) {
    return (
      <Alert severity="error">
        Dados de pagamento indisponíveis. Solicite uma nova cobrança ao seu treinador.
      </Alert>
    );
  }

  if (pagamento.status === "Falhou" || pagamento.status === "Expirado") {
    return (
      <Alert severity="error">
        {pagamento.status === "Falhou" ? "Pagamento falhou." : "Pagamento expirado."}{" "}
        Solicite uma nova cobrança ao seu treinador.
      </Alert>
    );
  }

  return (
    <Elements stripe={stripePromise} options={{ clientSecret: pagamento.clientSecret }}>
      <CartaoForm pagamento={pagamento} onPago={onPago} />
    </Elements>
  );
}
