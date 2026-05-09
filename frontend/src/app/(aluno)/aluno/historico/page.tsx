"use client";
import { useCallback } from "react";
import { Box, Typography } from "@mui/material";
import AlertBanner from "@/components/ui/AlertBanner";
import DataList from "@/components/ui/DataList";
import type { Column } from "@/components/ui/ResponsiveTable";
import { alunoApi } from "@/lib/api/aluno";
import type { ExecucaoTreinoResponse } from "@/types";
import { usePaginatedList } from "@/hooks/usePaginatedList";

const COLUMNS: Column[] = [
  { label: "Data" },
  { label: "Observação" },
];

export default function HistoricoAlunoPage() {
  const fetcher = useCallback(
    (p: number, ps: number) => alunoApi.listExecucoes({ pagina: p + 1, tamanhoPagina: ps }).then((r) => r.data),
    []
  );
  const { items: execucoes, total, page, pageSize, loading, error, setPage, setPageSize, setError } =
    usePaginatedList<ExecucaoTreinoResponse>({ fetcher, errorMessage: "Erro ao carregar histórico." });

  return (
    <Box>
      <Typography variant="h5" sx={{ fontWeight: 700, mb: 3 }}>Histórico de Sessões</Typography>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      <DataList
        loading={loading}
        items={execucoes}
        emptyMessage="Nenhuma sessão registrada. Execute uma ficha de treino para iniciar seu histórico de evolução."
        columns={COLUMNS}
        rowKey={(ex) => ex.execucaoId}
        pagination={{ count: total, page, rowsPerPage: pageSize, onPageChange: setPage, onRowsPerPageChange: setPageSize }}
        renderCell={(ex, i) => {
          if (i === 0) return (
            <>
              <Typography variant="body2" sx={{ fontWeight: 500 }}>
                {new Date(ex.dataExecucao).toLocaleDateString("pt-BR", {
                  weekday: "short", day: "2-digit", month: "short", year: "numeric",
                })}
              </Typography>
              <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                {new Date(ex.createdAt).toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" })}
              </Typography>
            </>
          );
          return <Typography variant="body2" color="text.secondary">{ex.observacao ?? "—"}</Typography>;
        }}
      />
    </Box>
  );
}
