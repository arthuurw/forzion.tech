"use client";
import { useEffect, useState } from "react";
import {
  Box, Typography, CircularProgress, Alert,
  Chip, Button, Dialog, DialogContent, DialogTitle, IconButton,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import type { PagamentoResponse } from "@/types";
import { pagamentoApi } from "@/lib/api/pagamento";
import { ResponsiveTable, type Column } from "@/components/ui/ResponsiveTable";
import { PAGAMENTO_STATUS_COLORS, PAGAMENTO_STATUS_LABEL } from "@/lib/constants/labels";
import PagamentoPix from "@/components/pagamento/PagamentoPix";
import PagamentoCartao from "@/components/pagamento/PagamentoCartao";
import { extractApiError } from "@/lib/api/extractApiError";

const COLUNAS: Column[] = [
  { label: "Data" },
  { label: "Valor" },
  { label: "Status" },
  { label: "Ações", align: "right" },
];

interface Props {
  pagamentos: PagamentoResponse[];
  loading: boolean;
  error: string;
  onAtualizar: () => void;
}

function TabelaPagamentos({ pagamentos, loading, error, onAtualizar }: Props) {
  const [pagamentoAberto, setPagamentoAberto] = useState<PagamentoResponse | null>(null);

  if (loading) return <CircularProgress />;
  if (error) return <Alert severity="error">{error}</Alert>;
  if (pagamentos.length === 0) return <Alert severity="info">Nenhum pagamento encontrado.</Alert>;

  return (
    <>
      <ResponsiveTable<PagamentoResponse>
        columns={COLUNAS}
        rows={pagamentos}
        rowKey={(p) => p.pagamentoId}
        renderCell={(p, col) => {
          switch (col) {
            case 0:
              return new Date(p.createdAt).toLocaleDateString("pt-BR");
            case 1:
              return p.valor.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
            case 2:
              return (
                <Chip
                  label={PAGAMENTO_STATUS_LABEL[p.status]}
                  color={PAGAMENTO_STATUS_COLORS[p.status]}
                  size="small"
                />
              );
            case 3:
              return p.status === "Pendente" ? (
                <Button size="small" onClick={() => setPagamentoAberto(p)}>
                  Pagar
                </Button>
              ) : null;
            default:
              return null;
          }
        }}
      />

      <Dialog open={!!pagamentoAberto} onClose={() => setPagamentoAberto(null)} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}>
        <DialogTitle>
          {pagamentoAberto?.metodoPagamento === "Cartao" ? "Pagamento com cartão" : "Pagamento via Pix"}
          <IconButton onClick={() => setPagamentoAberto(null)} sx={{ position: "absolute", right: 8, top: 8 }} aria-label="Fechar">
            <CloseIcon />
          </IconButton>
        </DialogTitle>
        <DialogContent sx={{ pt: 1 }}>
          {pagamentoAberto && pagamentoAberto.metodoPagamento === "Cartao" ? (
            <PagamentoCartao
              pagamentoId={pagamentoAberto.pagamentoId}
              onPago={() => { setPagamentoAberto(null); onAtualizar(); }}
            />
          ) : pagamentoAberto ? (
            <PagamentoPix
              pagamentoId={pagamentoAberto.pagamentoId}
              onPago={() => { setPagamentoAberto(null); onAtualizar(); }}
            />
          ) : null}
        </DialogContent>
      </Dialog>
    </>
  );
}

export default function PagamentosAlunoPage() {
  const [pagamentos, setPagamentos] = useState<PagamentoResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const carregar = async () => {
    setLoading(true);
    try {
      const assRes = await pagamentoApi.obterMinhaAssinatura();
      // 204 No Content → aluno não possui assinatura; lista vazia sem erro
      if (assRes.status === 204 || !assRes.data?.assinaturaAlunoId) {
        setPagamentos([]);
        return;
      }
      const pgRes = await pagamentoApi.listarPagamentosAssinatura(assRes.data.assinaturaAlunoId);
      setPagamentos(pgRes.data);
    } catch (err) {
      setError(extractApiError(err, "Erro ao carregar pagamentos."));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { void carregar(); }, []);

  return (
    <Box sx={{ p: { xs: 2, sm: 4 } }}>
      <Typography variant="h5" sx={{ fontWeight: "bold", mb: 3 }}>Histórico de Pagamentos</Typography>
      <TabelaPagamentos
        pagamentos={pagamentos}
        loading={loading}
        error={error}
        onAtualizar={carregar}
      />
    </Box>
  );
}
