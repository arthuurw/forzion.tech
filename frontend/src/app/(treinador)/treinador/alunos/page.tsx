"use client";
import { useCallback, useEffect, useState } from "react";
import {
  Box, Typography, Card, Table, TableHead, TableRow, TableCell, TableBody,
  TablePagination, Select, MenuItem, FormControl, InputLabel, IconButton,
  Tooltip, Dialog, DialogTitle, DialogContent, DialogActions, Button, Autocomplete, TextField,
} from "@mui/material";
import CheckIcon from "@mui/icons-material/Check";
import LinkOffIcon from "@mui/icons-material/LinkOff";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import { useRouter } from "next/navigation";
import StatusChip from "@/components/ui/StatusChip";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import { treinadorApi } from "@/lib/api/treinador";
import type { VinculoDetalheResponse, VinculoStatus, PacoteAlunoResponse } from "@/types";

export default function AlunosTreinadorPage() {
  const router = useRouter();
  const [vinculos, setVinculos] = useState<VinculoDetalheResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(10);
  const [statusFilter, setStatusFilter] = useState<VinculoStatus | "">("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [confirmDesvincular, setConfirmDesvincular] = useState<VinculoDetalheResponse | null>(null);
  const [loadingDesvincular, setLoadingDesvincular] = useState(false);
  const [observacaoDesvincular, setObservacaoDesvincular] = useState("");

  const [aprovarDialog, setAprovarDialog] = useState<VinculoDetalheResponse | null>(null);
  const [pacotes, setPacotes] = useState<PacoteAlunoResponse[]>([]);
  const [selectedPacote, setSelectedPacote] = useState<PacoteAlunoResponse | null>(null);
  const [loadingAprovar, setLoadingAprovar] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const res = await treinadorApi.listVinculos({
        status: statusFilter || undefined,
        pagina: page + 1,
        tamanhoPagina: pageSize,
      });
      setVinculos(res.data.items);
      setTotal(res.data.total);
    } catch {
      setError("Erro ao carregar alunos.");
    } finally {
      setLoading(false);
    }
  }, [statusFilter, page, pageSize]);

  useEffect(() => { load(); }, [load]);

  const openAprovar = async (v: VinculoDetalheResponse) => {
    setAprovarDialog(v);
    setSelectedPacote(null);
    if (pacotes.length === 0) {
      try {
        const res = await treinadorApi.listPacotes();
        setPacotes(res.data);
      } catch {
        setError("Erro ao carregar pacotes.");
      }
    }
  };

  const handleAprovar = async () => {
    if (!aprovarDialog || !selectedPacote) return;
    setLoadingAprovar(true);
    try {
      await treinadorApi.aprovarVinculo(aprovarDialog.vinculoId, selectedPacote.pacoteId);
      setSuccess(`${aprovarDialog.nomeAluno} aprovado com o pacote "${selectedPacote.nome}".`);
      setAprovarDialog(null);
      load();
    } catch {
      setError("Erro ao aprovar vínculo.");
    } finally {
      setLoadingAprovar(false);
    }
  };

  const handleDesvincular = async () => {
    if (!confirmDesvincular) return;
    setLoadingDesvincular(true);
    try {
      await treinadorApi.desvincularAluno(confirmDesvincular.vinculoId, observacaoDesvincular.trim() || null);
      setSuccess(`${confirmDesvincular.nomeAluno} desvinculado.`);
      setConfirmDesvincular(null);
      setObservacaoDesvincular("");
      load();
    } catch {
      setError("Erro ao desvincular aluno.");
    } finally {
      setLoadingDesvincular(false);
    }
  };

  return (
    <Box>
      <Typography variant="h5" sx={{ fontWeight: 700, mb: 3 }}>Alunos</Typography>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!success} severity="success" message={success} onClose={() => setSuccess("")} />

      <Box sx={{ mb: 2 }}>
        <FormControl size="small" sx={{ minWidth: 180 }}>
          <InputLabel>Status</InputLabel>
          <Select
            value={statusFilter}
            label="Status"
            onChange={(e) => { setStatusFilter(e.target.value as VinculoStatus | ""); setPage(0); }}
          >
            <MenuItem value="">Todos</MenuItem>
            <MenuItem value="AguardandoAprovacao">Aguardando</MenuItem>
            <MenuItem value="Ativo">Ativo</MenuItem>
            <MenuItem value="Inativo">Inativo</MenuItem>
          </Select>
        </FormControl>
      </Box>

      <Card variant="outlined">
        {loading ? <LoadingSpinner /> : vinculos.length === 0 ? (
          <EmptyState message="Nenhum aluno encontrado." />
        ) : (
          <>
            <Box sx={{ overflowX: "auto" }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 600 }}>Aluno</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Status</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Desde</TableCell>
                    <TableCell align="right" sx={{ fontWeight: 600 }}>Ações</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {vinculos.map((v) => (
                    <TableRow key={v.vinculoId} hover>
                      <TableCell>
                        <Typography variant="body2" sx={{ fontWeight: 500 }}>{v.nomeAluno}</Typography>
                        {v.emailAluno && (
                          <Typography variant="caption" color="text.secondary">{v.emailAluno}</Typography>
                        )}
                      </TableCell>
                      <TableCell><StatusChip status={v.status} /></TableCell>
                      <TableCell>
                        <Typography variant="caption">
                          {new Date(v.createdAt).toLocaleDateString("pt-BR")}
                        </Typography>
                      </TableCell>
                      <TableCell align="right">
                        {v.status === "AguardandoAprovacao" && (
                          <Tooltip title="Aprovar vínculo">
                            <IconButton size="small" color="success" onClick={() => openAprovar(v)}>
                              <CheckIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        )}
                        {v.status === "Ativo" && (
                          <>
                            <Tooltip title="Ver detalhes">
                              <IconButton size="small" onClick={() => router.push(`/treinador/alunos/${v.alunoId}`)}>
                                <OpenInNewIcon fontSize="small" />
                              </IconButton>
                            </Tooltip>
                            <Tooltip title="Desvincular">
                              <IconButton size="small" color="error" onClick={() => { setConfirmDesvincular(v); setObservacaoDesvincular(""); }}>
                                <LinkOffIcon fontSize="small" />
                              </IconButton>
                            </Tooltip>
                          </>
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

      {/* Aprovar com pacote */}
      <Dialog open={!!aprovarDialog} onClose={() => setAprovarDialog(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Aprovar — {aprovarDialog?.nomeAluno}</DialogTitle>
        <DialogContent sx={{ pt: 2 }}>
          <Autocomplete
            options={pacotes}
            getOptionLabel={(p) => `${p.nome} (até ${p.maxFichas} fichas)`}
            value={selectedPacote}
            onChange={(_, v) => setSelectedPacote(v)}
            renderInput={(params) => <TextField {...params} label="Pacote" size="small" />}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAprovarDialog(null)}>Cancelar</Button>
          <Button
            variant="contained"
            disabled={!selectedPacote || loadingAprovar}
            onClick={handleAprovar}
          >
            Aprovar
          </Button>
        </DialogActions>
      </Dialog>

      {/* Desvincular */}
      <ConfirmDialog
        open={!!confirmDesvincular}
        title="Desvincular aluno"
        description={`Desvincular "${confirmDesvincular?.nomeAluno}" irá inativar todas as fichas associadas. Deseja continuar?`}
        confirmLabel="Desvincular"
        destructive
        loading={loadingDesvincular}
        onConfirm={handleDesvincular}
        onClose={() => { setConfirmDesvincular(null); setObservacaoDesvincular(""); }}
      >
        <TextField
          label="Observacao (opcional)"
          value={observacaoDesvincular}
          onChange={(e) => setObservacaoDesvincular(e.target.value)}
          size="small"
          fullWidth
          multiline
          rows={3}
          sx={{ mt: 2 }}
        />
      </ConfirmDialog>
    </Box>
  );
}
