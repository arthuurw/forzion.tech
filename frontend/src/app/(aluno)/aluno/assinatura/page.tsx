"use client";
import { useEffect, useState } from "react";
import {
  Box, Typography, Paper, Stack, Chip, Button,
  CircularProgress, Alert, Divider,
} from "@mui/material";
import { alunoApi } from "@/lib/api/aluno";
import { pagamentoApi } from "@/lib/api/pagamento";
import type { AssinaturaResponse, PagamentoResponse } from "@/types";
import PagamentoPix from "@/components/pagamento/PagamentoPix";
import PagamentoCartao from "@/components/pagamento/PagamentoCartao";

const statusColor: Record<string, "default" | "success" | "warning" | "error"> = {
  Ativa: "success",
  Pendente: "warning",
  Inadimplente: "error",
  Cancelada: "default",
};

export default function AssinaturaAlunoPage() {
  const [assinatura, setAssinatura] = useState<AssinaturaResponse | null>(null);
  const [pagamentoPendente, setPagamentoPendente] = useState<PagamentoResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [mostrarPix, setMostrarPix] = useState(false);

  const carregar = async () => {
    setLoading(true);
    try {
      const vinculoRes = await alunoApi.getMeuVinculo();
      const vinculo = vinculoRes.data.vinculoAtivo;
      if (!vinculo) {
        setLoading(false);
        return;
      }

      // Busca assinatura pela assinaturaId — precisamos do endpoint de assinatura por vinculo
      // Por ora, busca pagamentos pendentes da assinatura via vinculo
      // TODO: adicionar endpoint GET /aluno/assinatura quando disponível
    } catch {
      setError("Erro ao carregar assinatura.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { carregar(); }, []);

  if (loading) return <Box sx={{ p: 4 }}><CircularProgress /></Box>;

  if (!assinatura) {
    return (
      <Box sx={{ p: 4 }}>
        <Alert severity="info">Você não possui assinatura ativa no momento.</Alert>
      </Box>
    );
  }

  return (
    <Box sx={{ p: 4, maxWidth: 500 }}>
      <Typography variant="h5" sx={{ fontWeight: "bold", mb: 3 }}>Minha Assinatura</Typography>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <Paper variant="outlined" sx={{ p: 3 }}>
        <Stack spacing={2}>
          <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center" }}>
            <Typography variant="subtitle1" sx={{ fontWeight: "medium" }}>Status</Typography>
            <Chip
              label={assinatura.status}
              color={statusColor[assinatura.status] ?? "default"}
              size="small"
            />
          </Stack>

          <Divider />

          <Stack direction="row" sx={{ justifyContent: "space-between" }}>
            <Typography variant="body2" color="text.secondary">Valor mensal</Typography>
            <Typography variant="body2" sx={{ fontWeight: "medium" }}>
              {assinatura.valor.toLocaleString("pt-BR", { style: "currency", currency: "BRL" })}
            </Typography>
          </Stack>

          <Stack direction="row" sx={{ justifyContent: "space-between" }}>
            <Typography variant="body2" color="text.secondary">Próxima cobrança</Typography>
            <Typography variant="body2" sx={{ fontWeight: "medium" }}>
              {new Date(assinatura.dataProximaCobranca).toLocaleDateString("pt-BR")}
            </Typography>
          </Stack>

          {pagamentoPendente && !mostrarPix && (
            <>
              <Divider />
              <Alert severity="warning" sx={{ py: 0.5 }}>
                Há um pagamento pendente.
              </Alert>
              <Button variant="contained" onClick={() => setMostrarPix(true)}>
                Pagar agora
              </Button>
            </>
          )}
        </Stack>
      </Paper>

      {mostrarPix && pagamentoPendente && (
        <Box sx={{ mt: 3 }}>
          {pagamentoPendente.metodoPagamento === "Cartao" ? (
            <PagamentoCartao
              pagamentoId={pagamentoPendente.pagamentoId}
              onPago={() => { setMostrarPix(false); carregar(); }}
            />
          ) : (
            <PagamentoPix
              pagamentoId={pagamentoPendente.pagamentoId}
              onPago={() => { setMostrarPix(false); carregar(); }}
            />
          )}
        </Box>
      )}
    </Box>
  );
}
