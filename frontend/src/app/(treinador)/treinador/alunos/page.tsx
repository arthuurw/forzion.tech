"use client";
import { useCallback, useEffect, useState } from "react";
import {
  Box, Typography, Card, Select, MenuItem, FormControl, InputLabel, IconButton,
  Tooltip, Dialog, DialogTitle, DialogContent, DialogActions, Button, Autocomplete, TextField,
  Checkbox, FormControlLabel,
} from "@mui/material";
import CheckIcon from "@mui/icons-material/Check";
import CloseIcon from "@mui/icons-material/Close";
import LinkOffIcon from "@mui/icons-material/LinkOff";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import ReplayIcon from "@mui/icons-material/Replay";
import { useRouter } from "next/navigation";
import StatusChip from "@/components/ui/StatusChip";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import { ResponsiveTable, type Column } from "@/components/ui/ResponsiveTable";
import { treinadorApi } from "@/lib/api/treinador";
import type { VinculoDetalheResponse, VinculoStatus, PacoteAlunoResponse } from "@/types";

const COLUMNS: Column[] = [
  { label: "Aluno" },
  { label: "Status" },
  { label: "Desde" },
  { label: "Ações", align: "right" },
];

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
  const [trarFichas, setTrarFichas] = useState(false);
  const [loadingAprovar, setLoadingAprovar] = useState(false);

  const [reativarDialog, setReativarDialog] = useState<VinculoDetalheResponse | null>(null);
  const [selectedPacoteReativar, setSelectedPacoteReativar] = useState<PacoteAlunoResponse | null>(null);
  const [loadingReativar, setLoadingReativar] = useState(false);

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

  const loadPacotes = async () => {
    if (pacotes.length === 0) {
      try {
        const res = await treinadorApi.listPacotes();
        setPacotes(res.data);
      } catch {
        setError("Erro ao carregar pacotes.");
      }
    }
  };

  const openAprovar = async (v: VinculoDetalheResponse) => {
    setAprovarDialog(v);
    setSelectedPacote(null);
    setTrarFichas(false);
    await loadPacotes();
  };

  const openReativar = async (v: VinculoDetalheResponse) => {
    setReativarDialog(v);
    setSelectedPacoteReativar(null);
    await loadPacotes();
  };

  const handleReativar = async () => {
    if (!reativarDialog || !selectedPacoteReativar) return;
    setLoadingReativar(true);
    try {
      await treinadorApi.reativarAluno(reativarDialog.alunoId, selectedPacoteReativar.pacoteId);
      setSuccess(`${reativarDialog.nomeAluno} reativado com o pacote "${selectedPacoteReativar.nome}".`);
      setReativarDialog(null);
      load();
    } catch {
      setError("Erro ao reativar aluno.");
    } finally {
      setLoadingReativar(false);
    }
  };

  const handleAprovar = async () => {
    if (!aprovarDialog || !selectedPacote) return;
    setLoadingAprovar(true);
    try {
      await treinadorApi.aprovarVinculo(aprovarDialog.vinculoId, selectedPacote.pacoteId, trarFichas);
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
          <ResponsiveTable
            columns={COLUMNS}
            rows={vinculos}
            rowKey={(v) => v.vinculoId}
            pagination={{
              count: total,
              page,
              rowsPerPage: pageSize,
              onPageChange: setPage,
              onRowsPerPageChange: (size) => { setPageSize(size); setPage(0); },
            }}
            renderCell={(v, i) => {
              if (i === 0) return (
                <>
                  <Typography variant="body2" sx={{ fontWeight: 500 }}>{v.nomeAluno}</Typography>
                  {v.emailAluno && (
                    <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                      {v.emailAluno}
                    </Typography>
                  )}
                </>
              );
              if (i === 1) return <StatusChip status={v.status} />;
              if (i === 2) return (
                <Typography variant="caption">
                  {new Date(v.createdAt).toLocaleDateString("pt-BR")}
                </Typography>
              );
              return (
                <>
                  {v.status === "AguardandoAprovacao" && (
                    <>
                      <Tooltip title="Aprovar vínculo">
                        <IconButton size="small" color="success" onClick={() => openAprovar(v)}>
                          <CheckIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                      <Tooltip title="Reprovar vínculo">
                        <IconButton
                          size="small"
                          color="error"
                          onClick={() => { setConfirmDesvincular(v); setObservacaoDesvincular(""); }}
                        >
                          <CloseIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    </>
                  )}
                  {v.status === "Ativo" && (
                    <>
                      <Tooltip title="Ver detalhes">
                        <IconButton size="small" onClick={() => router.push(`/treinador/alunos/${v.alunoId}`)}>
                          <OpenInNewIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                      <Tooltip title="Desvincular">
                        <IconButton
                          size="small"
                          color="error"
                          onClick={() => { setConfirmDesvincular(v); setObservacaoDesvincular(""); }}
                        >
                          <LinkOffIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    </>
                  )}
                  {v.status === "Inativo" && (
                    <Tooltip title="Reativar aluno">
                      <IconButton size="small" color="primary" onClick={() => openReativar(v)}>
                        <ReplayIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  )}
                </>
              );
            }}
          />
        )}
      </Card>

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
          {aprovarDialog?.temVinculoAtivoPrevio && (
            <FormControlLabel
              sx={{ mt: 1.5 }}
              control={
                <Checkbox
                  size="small"
                  checked={trarFichas}
                  onChange={(e) => setTrarFichas(e.target.checked)}
                />
              }
              label={
                <Typography variant="body2">
                  Trazer fichas ativas do treinador anterior
                </Typography>
              }
            />
          )}
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

      <Dialog open={!!reativarDialog} onClose={() => setReativarDialog(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Reativar — {reativarDialog?.nomeAluno}</DialogTitle>
        <DialogContent sx={{ pt: 2 }}>
          <Autocomplete
            options={pacotes}
            getOptionLabel={(p) => `${p.nome} (até ${p.maxFichas} fichas)`}
            value={selectedPacoteReativar}
            onChange={(_, v) => setSelectedPacoteReativar(v)}
            renderInput={(params) => <TextField {...params} label="Pacote" size="small" />}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setReativarDialog(null)}>Cancelar</Button>
          <Button
            variant="contained"
            disabled={!selectedPacoteReativar || loadingReativar}
            onClick={handleReativar}
          >
            Reativar
          </Button>
        </DialogActions>
      </Dialog>

      <ConfirmDialog
        open={!!confirmDesvincular}
        title={confirmDesvincular?.status === "AguardandoAprovacao" ? "Reprovar vínculo" : "Desvincular aluno"}
        description={
          confirmDesvincular?.status === "AguardandoAprovacao"
            ? `Reprovar o vínculo de "${confirmDesvincular?.nomeAluno}"? O aluno não poderá mais acessar seus treinos.`
            : `Desvincular "${confirmDesvincular?.nomeAluno}" irá inativar todas as fichas associadas. Deseja continuar?`
        }
        confirmLabel={confirmDesvincular?.status === "AguardandoAprovacao" ? "Reprovar" : "Desvincular"}
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
