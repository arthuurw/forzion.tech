"use client";
import { useCallback, useEffect, useState } from "react";
import {
  Box, Typography, Card, Table, TableHead, TableRow, TableCell, TableBody,
  TablePagination,
} from "@mui/material";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import { alunoApi } from "@/lib/api/aluno";
import type { ExecucaoTreinoResponse } from "@/types";

export default function HistoricoAlunoPage() {
  const [execucoes, setExecucoes] = useState<ExecucaoTreinoResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(10);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const res = await alunoApi.listExecucoes({ pagina: page + 1, tamanhoPagina: pageSize });
      setExecucoes(res.data.items);
      setTotal(res.data.total);
    } catch {
      setError("Erro ao carregar histórico.");
    } finally {
      setLoading(false);
    }
  }, [page, pageSize]);

  useEffect(() => { load(); }, [load]);

  return (
    <Box>
      <Typography variant="h5" sx={{ fontWeight: 700, mb: 3 }}>Histórico de Treinos</Typography>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      <Card variant="outlined">
        {loading ? (
          <LoadingSpinner />
        ) : execucoes.length === 0 ? (
          <EmptyState message="Nenhum treino registrado ainda. Complete uma ficha para ver seu histórico." />
        ) : (
          <>
            <Box sx={{ overflowX: "auto" }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 600 }}>Data</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Observação</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {execucoes.map((ex) => (
                    <TableRow key={ex.execucaoId} hover>
                      <TableCell>
                        <Typography variant="body2" sx={{ fontWeight: 500 }}>
                          {new Date(ex.dataExecucao).toLocaleDateString("pt-BR", {
                            weekday: "short",
                            day: "2-digit",
                            month: "short",
                            year: "numeric",
                          })}
                        </Typography>
                        <Typography variant="caption" color="text.secondary">
                          {new Date(ex.createdAt).toLocaleTimeString("pt-BR", {
                            hour: "2-digit",
                            minute: "2-digit",
                          })}
                        </Typography>
                      </TableCell>
                      <TableCell>
                        <Typography variant="body2" color="text.secondary">
                          {ex.observacao ?? "—"}
                        </Typography>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Box>
            <TablePagination
              component="div"
              count={total}
              page={page}
              rowsPerPage={pageSize}
              onPageChange={(_, p) => setPage(p)}
              onRowsPerPageChange={(e) => { setPageSize(+e.target.value); setPage(0); }}
              rowsPerPageOptions={[5, 10, 25]}
              labelRowsPerPage="Por página:"
              labelDisplayedRows={({ from, to, count }) => `${from}–${to} de ${count}`}
            />
          </>
        )}
      </Card>
    </Box>
  );
}
