"use client";
import { useCallback } from "react";
import {
  Box, Typography, Chip, IconButton, Tooltip,
} from "@mui/material";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import { useRouter } from "next/navigation";
import StatusChip from "@/components/ui/StatusChip";
import AlertBanner from "@/components/ui/AlertBanner";
import DataList from "@/components/ui/DataList";
import type { Column } from "@/components/ui/ResponsiveTable";
import { alunoApi, type TreinoAlunoDetalheResponse } from "@/lib/api/aluno";
import { usePaginatedList } from "@/hooks/usePaginatedList";
import { OBJETIVO_LABEL } from "@/lib/constants/labels";

const COLUMNS: Column[] = [
  { label: "Ficha" },
  { label: "Objetivo" },
  { label: "Exercícios" },
  { label: "Status" },
  { label: "Ações", align: "right" },
];

export default function FichasAlunoPage() {
  const router = useRouter();
  const fetcher = useCallback(
    (p: number, ps: number) => alunoApi.listFichas({ pagina: p + 1, tamanhoPagina: ps }).then((r) => r.data),
    []
  );
  const { items: fichas, total, page, pageSize, loading, error, setPage, setPageSize, setError } =
    usePaginatedList<TreinoAlunoDetalheResponse>({ fetcher, errorMessage: "Erro ao carregar fichas." });

  return (
    <Box>
      <Typography variant="h5" sx={{ fontWeight: 700, mb: 3 }}>Fichas de Treino</Typography>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      <DataList
        loading={loading}
        items={fichas}
        emptyMessage="Nenhum protocolo de treino disponível. Quem te treina ainda não vinculou fichas à sua conta."
        columns={COLUMNS}
        rowKey={(f) => f.treinoAlunoId}
        onRowClick={(f) => router.push(`/aluno/fichas/${f.treinoAlunoId}`)}
        pagination={{ count: total, page, rowsPerPage: pageSize, onPageChange: setPage, onRowsPerPageChange: setPageSize }}
        renderCell={(f, i) => {
          if (i === 0) return <Typography variant="body2" sx={{ fontWeight: 500 }}>{f.nomeTreino}</Typography>;
          if (i === 1) return <Chip label={OBJETIVO_LABEL[f.objetivo] ?? f.objetivo} size="small" variant="outlined" />;
          if (i === 2) return f.exercicios.length;
          if (i === 3) return <StatusChip status={f.status} />;
          return (
            <>
              <Tooltip title="Ver ficha">
                <IconButton size="small" onClick={() => router.push(`/aluno/fichas/${f.treinoAlunoId}`)}>
                  <OpenInNewIcon fontSize="small" />
                </IconButton>
              </Tooltip>
              {f.status === "Ativo" && (
                <Tooltip title="Iniciar treino">
                  <IconButton size="small" color="primary" onClick={() => router.push(`/aluno/fichas/${f.treinoAlunoId}/executar`)}>
                    <PlayArrowIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              )}
            </>
          );
        }}
      />
    </Box>
  );
}
