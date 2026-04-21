"use client";
import { useCallback, useEffect, useState } from "react";
import {
  Box, Typography, Card, Chip, IconButton, Tooltip,
} from "@mui/material";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import { useRouter } from "next/navigation";
import StatusChip from "@/components/ui/StatusChip";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import { ResponsiveTable, type Column } from "@/components/ui/ResponsiveTable";
import { alunoApi, type TreinoAlunoDetalheResponse } from "@/lib/api/aluno";

const COLUMNS: Column[] = [
  { label: "Ficha" },
  { label: "Objetivo" },
  { label: "Exercícios" },
  { label: "Status" },
  { label: "Ações", align: "right" },
];

export default function FichasAlunoPage() {
  const router = useRouter();
  const [fichas, setFichas] = useState<TreinoAlunoDetalheResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(10);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const res = await alunoApi.listFichas({ pagina: page + 1, tamanhoPagina: pageSize });
      setFichas(res.data.items);
      setTotal(res.data.total);
    } catch {
      setError("Erro ao carregar fichas.");
    } finally {
      setLoading(false);
    }
  }, [page, pageSize]);

  useEffect(() => { load(); }, [load]);

  return (
    <Box>
      <Typography variant="h5" sx={{ fontWeight: 700, mb: 3 }}>Fichas de Treino</Typography>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      <Card variant="outlined">
        {loading ? (
          <LoadingSpinner />
        ) : fichas.length === 0 ? (
          <EmptyState message="Nenhum protocolo de treino disponível. Seu treinador ainda não vinculou fichas à sua conta." />
        ) : (
          <ResponsiveTable
            columns={COLUMNS}
            rows={fichas}
            rowKey={(f) => f.treinoAlunoId}
            onRowClick={(f) => router.push(`/aluno/fichas/${f.treinoAlunoId}`)}
            pagination={{
              count: total,
              page,
              rowsPerPage: pageSize,
              onPageChange: setPage,
              onRowsPerPageChange: (size) => { setPageSize(size); setPage(0); },
            }}
            renderCell={(f, i) => {
              if (i === 0) return <Typography variant="body2" sx={{ fontWeight: 500 }}>{f.nomeTreino}</Typography>;
              if (i === 1) return <Chip label={f.objetivo} size="small" variant="outlined" />;
              if (i === 2) return f.exercicios.length;
              if (i === 3) return <StatusChip status={f.status} />;
              return (
                <>
                  <Tooltip title="Ver ficha">
                    <IconButton
                      size="small"
                      onClick={() => router.push(`/aluno/fichas/${f.treinoAlunoId}`)}
                    >
                      <OpenInNewIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  {f.status === "Ativo" && (
                    <Tooltip title="Iniciar treino">
                      <IconButton
                        size="small"
                        color="primary"
                        onClick={() => router.push(`/aluno/fichas/${f.treinoAlunoId}/executar`)}
                      >
                        <PlayArrowIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  )}
                </>
              );
            }}
          />
        )}
      </Card>
    </Box>
  );
}
