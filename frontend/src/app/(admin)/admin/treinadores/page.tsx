"use client";
import { useEffect, useState, useCallback } from "react";
import {
  Box, Typography, Card, Table, TableHead, TableRow, TableCell,
  TableBody, TablePagination, Select, MenuItem, FormControl,
  InputLabel, IconButton, Tooltip, Dialog, DialogTitle,
  DialogContent, DialogActions, Button, Autocomplete, TextField,
} from "@mui/material";
import CheckIcon from "@mui/icons-material/Check";
import BlockIcon from "@mui/icons-material/Block";
import CardMembershipIcon from "@mui/icons-material/CardMembership";
import StatusChip from "@/components/ui/StatusChip";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import { adminApi } from "@/lib/api/admin";
import type { TreinadorResponse, TreinadorStatus, PlanoTreinadorResponse } from "@/types";

export default function TreinadoresAdminPage() {
  const [treinadores, setTreinadores] = useState<TreinadorResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(10);
  const [statusFilter, setStatusFilter] = useState<TreinadorStatus | "">("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [confirmAprovar, setConfirmAprovar] = useState<TreinadorResponse | null>(null);
  const [loadingAprovar, setLoadingAprovar] = useState(false);
  const [observacaoAprovar, setObservacaoAprovar] = useState("");

  const [confirmInativar, setConfirmInativar] = useState<TreinadorResponse | null>(null);
  const [loadingInativar, setLoadingInativar] = useState(false);
  const [observacaoInativar, setObservacaoInativar] = useState("");

  const [planoDialog, setPlanoDialog] = useState<TreinadorResponse | null>(null);
  const [planos, setPlanos] = useState<PlanoTreinadorResponse[]>([]);
  const [selectedPlano, setSelectedPlano] = useState<PlanoTreinadorResponse | null>(null);
  const [loadingPlano, setLoadingPlano] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const res = await adminApi.listTreinadores({
        status: statusFilter || undefined,
        pagina: page + 1,
        tamanhoPagina: pageSize,
      });
      setTreinadores(res.data.items);
      setTotal(res.data.total);
    } catch {
      setError("Erro ao carregar treinadores.");
    } finally {
      setLoading(false);
    }
  }, [statusFilter, page, pageSize]);

  useEffect(() => { load(); }, [load]);

  const handleAprovar = async () => {
    if (!confirmAprovar) return;
    setLoadingAprovar(true);
    try {
      await adminApi.aprovarTreinador(confirmAprovar.treinadorId, observacaoAprovar.trim() || null);
      setSuccess(`${confirmAprovar.nome} aprovado com sucesso.`);
      setConfirmAprovar(null);
      setObservacaoAprovar("");
      load();
    } catch {
      setError("Erro ao aprovar treinador.");
    } finally {
      setLoadingAprovar(false);
    }
  };

  const handleInativar = async () => {
    if (!confirmInativar) return;
    setLoadingInativar(true);
    try {
      await adminApi.inativarTreinador(confirmInativar.treinadorId, observacaoInativar.trim() || null);
      setSuccess(`${confirmInativar.nome} inativado. Vinculos e fichas afetados.`);
      setConfirmInativar(null);
      setObservacaoInativar("");
      load();
    } catch {
      setError("Erro ao inativar treinador.");
    } finally {
      setLoadingInativar(false);
    }
  };

  const openPlanoDialog = async (t: TreinadorResponse) => {
    setPlanoDialog(t);
    setSelectedPlano(null);
    if (planos.length === 0) {
      try {
        const res = await adminApi.listPlanos();
        setPlanos(res.data);
      } catch {
        setError("Erro ao carregar planos.");
      }
    }
  };

  const handleAtribuirPlano = async () => {
    if (!planoDialog || !selectedPlano) return;
    setLoadingPlano(true);
    try {
      await adminApi.atribuirPlano(planoDialog.treinadorId, selectedPlano.planoId);
      setSuccess(`Plano "${selectedPlano.nome}" atribuido a ${planoDialog.nome}.`);
      setPlanoDialog(null);
      load();
    } catch {
      setError("Erro ao atribuir plano.");
    } finally {
      setLoadingPlano(false);
    }
  };

  return (
    <Box>
      <Typography variant="h5" sx={{ fontWeight: 700, mb: 3 }}>
        Treinadores
      </Typography>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!success} severity="success" message={success} onClose={() => setSuccess("")} />

      <Box sx={{ mb: 2 }}>
        <FormControl size="small" sx={{ minWidth: 180 }}>
          <InputLabel>Status</InputLabel>
          <Select
            value={statusFilter}
            label="Status"
            onChange={(e) => { setStatusFilter(e.target.value as TreinadorStatus | ""); setPage(0); }}
          >
            <MenuItem value="">Todos</MenuItem>
            <MenuItem value="AguardandoAprovacao">Aguardando</MenuItem>
            <MenuItem value="Ativo">Ativo</MenuItem>
            <MenuItem value="Inativo">Inativo</MenuItem>
          </Select>
        </FormControl>
      </Box>

      <Card variant="outlined">
        {loading ? (
          <LoadingSpinner />
        ) : treinadores.length === 0 ? (
          <EmptyState message="Nenhum treinador encontrado." />
        ) : (
          <>
            <Box sx={{ overflowX: "auto" }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 600 }}>Nome</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Status</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Plano</TableCell>
                    <TableCell sx={{ fontWeight: 600 }}>Cadastro</TableCell>
                    <TableCell align="right" sx={{ fontWeight: 600 }}>Acoes</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {treinadores.map((t) => (
                    <TableRow key={t.treinadorId} hover>
                      <TableCell>{t.nome}</TableCell>
                      <TableCell><StatusChip status={t.status} /></TableCell>
                      <TableCell>
                        {t.planoTreinadorId ? (
                          <Typography variant="caption" color="text.secondary">Atribuido</Typography>
                        ) : (
                          <Typography variant="caption" color="text.disabled">-</Typography>
                        )}
                      </TableCell>
                      <TableCell>
                        <Typography variant="caption">
                          {new Date(t.createdAt).toLocaleDateString("pt-BR")}
                        </Typography>
                      </TableCell>
                      <TableCell align="right">
                        {t.status === "AguardandoAprovacao" && (
                          <Tooltip title="Aprovar">
                            <IconButton size="small" color="success" onClick={() => setConfirmAprovar(t)}>
                              <CheckIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        )}
                        {t.status === "Ativo" && (
                          <Tooltip title="Inativar">
                            <IconButton size="small" color="error" onClick={() => setConfirmInativar(t)}>
                              <BlockIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        )}
                        <Tooltip title="Atribuir plano">
                          <IconButton size="small" onClick={() => openPlanoDialog(t)}>
                            <CardMembershipIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
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
              labelRowsPerPage="Por pagina:"
              labelDisplayedRows={({ from, to, count }) => `${from}-${to} de ${count}`}
            />
          </>
        )}
      </Card>

      <ConfirmDialog
        open={!!confirmAprovar}
        title="Aprovar treinador"
        description={`Confirma a aprovacao de "${confirmAprovar?.nome}"?`}
        confirmLabel="Aprovar"
        loading={loadingAprovar}
        onConfirm={handleAprovar}
        onClose={() => { setConfirmAprovar(null); setObservacaoAprovar(""); }}
      >
        <TextField
          label="Observacao (opcional)"
          value={observacaoAprovar}
          onChange={(e) => setObservacaoAprovar(e.target.value)}
          size="small"
          fullWidth
          multiline
          rows={3}
          sx={{ mt: 2 }}
        />
      </ConfirmDialog>

      <ConfirmDialog
        open={!!confirmInativar}
        title="Inativar treinador"
        description={`Inativar "${confirmInativar?.nome}" ira desvincular todos os alunos e inativar as fichas associadas. Deseja continuar?`}
        confirmLabel="Inativar"
        destructive
        loading={loadingInativar}
        onConfirm={handleInativar}
        onClose={() => { setConfirmInativar(null); setObservacaoInativar(""); }}
      >
        <TextField
          label="Observacao (opcional)"
          value={observacaoInativar}
          onChange={(e) => setObservacaoInativar(e.target.value)}
          size="small"
          fullWidth
          multiline
          rows={3}
          sx={{ mt: 2 }}
        />
      </ConfirmDialog>

      <Dialog open={!!planoDialog} onClose={() => setPlanoDialog(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Atribuir plano - {planoDialog?.nome}</DialogTitle>
        <DialogContent sx={{ pt: 2 }}>
          <Autocomplete
            options={planos}
            getOptionLabel={(p) => `${p.nome} (ate ${p.maxAlunos} alunos)`}
            value={selectedPlano}
            onChange={(_, v) => setSelectedPlano(v)}
            renderInput={(params) => <TextField {...params} label="Plano" size="small" />}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPlanoDialog(null)}>Cancelar</Button>
          <Button
            variant="contained"
            disabled={!selectedPlano || loadingPlano}
            onClick={handleAtribuirPlano}
          >
            Confirmar
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
