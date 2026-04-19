"use client";
import { useCallback, useEffect, useState } from "react";
import {
  Box, Typography, Card, Table, TableHead, TableRow, TableCell, TableBody,
  TablePagination, Chip, IconButton, Tooltip,
} from "@mui/material";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import { useRouter } from "next/navigation";
import StatusChip from "@/components/ui/StatusChip";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import { alunoApi, type TreinoAlunoDetalheResponse } from "@/lib/api/aluno";

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
      <Typography variant="h5" sx={{ fontWeight: 700, mb: 3 }}>Minhas Fichas</Typography>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      <Card variant="outlined">
        {loading ? (
          <LoadingSpinner />
        ) : fichas.length === 0 ? (
          <EmptyState message="Nenhuma ficha vinculada. Aguarde seu treinador vincular uma ficha." />
        ) : (
          <>
            <Box sx={{ overflowX: "auto" }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 600 }}>Ficha</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Objetivo</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Exercícios</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Status</TableCell>
                    <TableCell align="right" sx={{ fontWeight: 600 }}>Ações</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {fichas.map((f) => (
                    <TableRow
                      key={f.treinoAlunoId}
                      hover
                      sx={{ cursor: "pointer" }}
                      onClick={() => router.push(`/aluno/fichas/${f.treinoAlunoId}`)}
                    >
                      <TableCell sx={{ fontWeight: 500 }}>{f.nomeTreino}</TableCell>
                      <TableCell>
                        <Chip label={f.objetivo} size="small" variant="outlined" />
                      </TableCell>
                      <TableCell>{f.exercicios.length}</TableCell>
                      <TableCell><StatusChip status={f.status} /></TableCell>
                      <TableCell align="right" onClick={(e) => e.stopPropagation()}>
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
