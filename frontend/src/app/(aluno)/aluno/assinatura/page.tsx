"use client";
import { useEffect, useState } from "react";
import {
  Box, Typography, Paper, Stack, Chip, Button,
  CircularProgress, Alert, Divider,
} from "@mui/material";
import { pagamentoApi } from "@/lib/api/pagamento";
import { extractApiError } from "@/lib/api/extractApiError";
import { baixarMeusDados as baixarMeusDadosBlob } from "@/lib/utils/downloadBlob";
import type { AssinaturaAlunoResponse, PagamentoResponse } from "@/types";
import PagamentoPix from "@/components/pagamento/PagamentoPix";
import PagamentoCartao from "@/components/pagamento/PagamentoCartao";
import ConfirmDialog from "@/components/ui/ConfirmDialog";

const statusColor: Record<string, "default" | "success" | "warning" | "error"> = {
  Ativa: "success",
  Pendente: "warning",
  Inadimplente: "error",
  Cancelada: "default",
};

// Texto verbatim do ConfirmDialog — fonte da verdade para o conteúdo da
// confirmação (também referenciado pelos testes).
export const CANCELAR_ASSINATURA_DESCRICAO =
  "Sua assinatura será cancelada imediatamente. Você não terá mais acesso a novas execuções e fichas serão somente leitura. Esta ação NÃO pode ser desfeita pelo portal — para reativar, contate quem te treina.";

export default function AssinaturaAlunoPage() {
  const [assinatura, setAssinatura] = useState<AssinaturaAlunoResponse | null>(null);
  const [pagamentoPendente, setPagamentoPendente] = useState<PagamentoResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [mostrarPix, setMostrarPix] = useState(false);
  const [confirmarCancelar, setConfirmarCancelar] = useState(false);
  const [cancelando, setCancelando] = useState(false);
  const [baixandoDados, setBaixandoDados] = useState<false | "xlsx" | "json">(false);
  const [sucesso, setSucesso] = useState("");

  const carregar = async () => {
    setLoading(true);
    try {
      const assRes = await pagamentoApi.obterMinhaAssinatura();
      // 204 No Content → aluno não possui assinatura
      if (assRes.status === 204 || !assRes.data?.assinaturaAlunoId) {
        setAssinatura(null);
        setPagamentoPendente(null);
        return;
      }
      setAssinatura(assRes.data);

      const pgRes = await pagamentoApi.listarPagamentosAssinatura(assRes.data.assinaturaAlunoId);
      setPagamentoPendente(pgRes.data.items.find(p => p.status === "Pendente") ?? null);
    } catch (err) {
      setError(extractApiError(err, "Erro ao carregar assinatura."));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { carregar(); }, []);

  const cancelarAssinatura = async () => {
    setCancelando(true);
    setError("");
    try {
      await pagamentoApi.cancelarMinhaAssinatura();
      setSucesso("Assinatura cancelada com sucesso.");
      setConfirmarCancelar(false);
      await carregar();
    } catch (err) {
      setError(extractApiError(err, "Não foi possível cancelar a assinatura. Tente novamente."));
    } finally {
      setCancelando(false);
    }
  };

  const baixarMeusDados = async (formato: "xlsx" | "json") => {
    setBaixandoDados(formato);
    setError("");
    try {
      await baixarMeusDadosBlob(formato);
    } catch (err) {
      setError(extractApiError(err, "Erro ao exportar dados."));
    } finally {
      setBaixandoDados(false);
    }
  };

  if (loading) return <Box sx={{ p: { xs: 2, md: 4 } }}><CircularProgress /></Box>;

  if (!assinatura) {
    return (
      <Box sx={{ p: { xs: 2, md: 4 } }}>
        <Alert severity="info">Você não possui assinatura ativa no momento.</Alert>
      </Box>
    );
  }

  const podeCancelar = assinatura.status === "Ativa" || assinatura.status === "Inadimplente";

  return (
    <Box sx={{ maxWidth: { xs: "100%", md: 500 } }}>
      <Typography variant="h5" sx={{ fontWeight: "bold", mb: 3 }}>Minha Assinatura</Typography>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      {sucesso && <Alert severity="success" sx={{ mb: 2 }}>{sucesso}</Alert>}

      <Paper variant="outlined" sx={{ p: 3 }}>
        <Stack spacing={2}>
          <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1 }}>
            <Typography variant="subtitle1" sx={{ fontWeight: "medium" }}>Status</Typography>
            <Chip
              label={assinatura.status}
              color={statusColor[assinatura.status] ?? "default"}
              size="small"
            />
          </Stack>

          <Divider />

          <Stack direction="row" sx={{ justifyContent: "space-between", flexWrap: "wrap", gap: 1 }}>
            <Typography variant="body2" color="text.secondary">Valor mensal</Typography>
            <Typography variant="body2" sx={{ fontWeight: "medium" }}>
              {assinatura.valor.toLocaleString("pt-BR", { style: "currency", currency: "BRL" })}
            </Typography>
          </Stack>

          <Stack direction="row" sx={{ justifyContent: "space-between", flexWrap: "wrap", gap: 1 }}>
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

          {podeCancelar && (
            <>
              <Divider />
              <Button
                variant="outlined"
                color="error"
                onClick={() => setConfirmarCancelar(true)}
              >
                Cancelar assinatura
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

      <ConfirmDialog
        open={confirmarCancelar}
        title="Cancelar assinatura"
        description={CANCELAR_ASSINATURA_DESCRICAO}
        destructive
        confirmLabel="Cancelar assinatura"
        cancelLabel="Voltar"
        loading={cancelando}
        onConfirm={cancelarAssinatura}
        onClose={() => { if (!cancelando) setConfirmarCancelar(false); }}
      >
        <Stack direction={{ xs: "column", sm: "row" }} spacing={1} sx={{ flexWrap: "wrap", mt: 1 }}>
          <Button
            variant="text"
            size="small"
            onClick={() => baixarMeusDados("xlsx")}
            disabled={!!baixandoDados}
            startIcon={baixandoDados === "xlsx" ? <CircularProgress size={14} color="inherit" /> : undefined}
          >
            Baixar meus dados (Excel)
          </Button>
          <Button
            variant="text"
            size="small"
            onClick={() => baixarMeusDados("json")}
            disabled={!!baixandoDados}
            startIcon={baixandoDados === "json" ? <CircularProgress size={14} color="inherit" /> : undefined}
          >
            Baixar como JSON
          </Button>
        </Stack>
      </ConfirmDialog>
    </Box>
  );
}
