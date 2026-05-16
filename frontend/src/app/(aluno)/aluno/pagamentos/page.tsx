"use client";
import { useState } from "react";
import {
  Box, Typography, CircularProgress, Alert,
  Table, TableBody, TableCell, TableHead, TableRow,
  Chip, Button, Dialog, DialogContent, DialogTitle, IconButton,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import type { PagamentoResponse, PagamentoStatus } from "@/types";
import PagamentoPix from "@/components/pagamento/PagamentoPix";
import PagamentoCartao from "@/components/pagamento/PagamentoCartao";

const statusColor: Record<PagamentoStatus, "default" | "success" | "warning" | "error"> = {
  Pago: "success",
  Pendente: "warning",
  Expirado: "default",
  Falhou: "error",
};

const statusLabel: Record<PagamentoStatus, string> = {
  Pago: "Pago",
  Pendente: "Pendente",
  Expirado: "Expirado",
  Falhou: "Falhou",
};

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
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Data</TableCell>
            <TableCell>Valor</TableCell>
            <TableCell>Status</TableCell>
            <TableCell />
          </TableRow>
        </TableHead>
        <TableBody>
          {pagamentos.map((p) => (
            <TableRow key={p.pagamentoId}>
              <TableCell>{new Date(p.createdAt).toLocaleDateString("pt-BR")}</TableCell>
              <TableCell>
                {p.valor.toLocaleString("pt-BR", { style: "currency", currency: "BRL" })}
              </TableCell>
              <TableCell>
                <Chip
                  label={statusLabel[p.status]}
                  color={statusColor[p.status]}
                  size="small"
                />
              </TableCell>
              <TableCell>
                {p.status === "Pendente" && (
                  <Button size="small" onClick={() => setPagamentoAberto(p)}>
                    Pagar
                  </Button>
                )}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>

      <Dialog open={!!pagamentoAberto} onClose={() => setPagamentoAberto(null)} maxWidth="xs" fullWidth>
        <DialogTitle>
          {pagamentoAberto?.metodoPagamento === "Cartao" ? "Pagamento com cartão" : "Pagamento via Pix"}
          <IconButton onClick={() => setPagamentoAberto(null)} sx={{ position: "absolute", right: 8, top: 8 }}>
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
  // Sem assinaturaId por params aqui — página geral de histórico
  // A integração completa requer buscar assinaturas do aluno primeiro
  const [pagamentos] = useState<PagamentoResponse[]>([]);
  const [loading] = useState(false);
  const [error] = useState("");

  return (
    <Box p={4}>
      <Typography variant="h5" fontWeight="bold" mb={3}>Histórico de Pagamentos</Typography>
      <TabelaPagamentos
        pagamentos={pagamentos}
        loading={loading}
        error={error}
        onAtualizar={() => {}}
      />
    </Box>
  );
}
