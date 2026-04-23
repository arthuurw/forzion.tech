"use client";
import { useCallback, useEffect, useState } from "react";
import { Box, Typography, Card } from "@mui/material";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import { ResponsiveTable, type Column } from "@/components/ui/ResponsiveTable";
import { alunoApi } from "@/lib/api/aluno";
import type { ExecucaoTreinoResponse } from "@/types";

const COLUMNS: Column[] = [
  { label: "Data" },
  { label: "Observação" },
];

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
      <Typography variant="h5" sx={{ fontWeight: 700, mb: 3 }}>Histórico de Sessões</Typography>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      <Card variant="outlined">
        {loading ? (
          <LoadingSpinner />
        ) : execucoes.length === 0 ? (
          <EmptyState message="Nenhuma sessão registrada. Execute uma ficha de treino para iniciar seu histórico de evolução." />
        ) : (
          <ResponsiveTable
            columns={COLUMNS}
            rows={execucoes}
            rowKey={(ex) => ex.execucaoId}
            pagination={{
              count: total,
              page,
              rowsPerPage: pageSize,
              onPageChange: setPage,
              onRowsPerPageChange: (size) => { setPageSize(size); setPage(0); },
            }}
            renderCell={(ex, i) => {
              if (i === 0) return (
                <>
                  <Typography variant="body2" sx={{ fontWeight: 500 }}>
                    {new Date(ex.dataExecucao).toLocaleDateString("pt-BR", {
                      weekday: "short",
                      day: "2-digit",
                      month: "short",
                      year: "numeric",
                    })}
                  </Typography>
                  <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                    {new Date(ex.createdAt).toLocaleTimeString("pt-BR", {
                      hour: "2-digit",
                      minute: "2-digit",
                    })}
                  </Typography>
                </>
              );
              return (
                <Typography variant="body2" color="text.secondary">
                  {ex.observacao ?? "—"}
                </Typography>
              );
            }}
          />
        )}
      </Card>
    </Box>
  );
}
