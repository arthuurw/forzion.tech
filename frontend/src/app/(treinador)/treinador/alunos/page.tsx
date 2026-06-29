"use client";
import { useCallback, useState } from "react";
import {
  Box, Typography, Select, MenuItem, FormControl, InputLabel, IconButton,
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
import PageHeader from "@/components/ui/PageHeader";
import DataList from "@/components/ui/DataList";
import type { Column } from "@/components/ui/ResponsiveTable";
import { treinadorApi } from "@/lib/api/treinador";
import { extractApiError } from "@/lib/api/extractApiError";
import type { VinculoDetalheResponse, VinculoStatus, PacoteResponse } from "@/types";
import { usePaginatedList } from "@/hooks/usePaginatedList";

const COLUMNS: Column[] = [
  { label: "Aluno" },
  { label: "Status" },
  { label: "Desde" },
  { label: "Ações", align: "right" },
];

export default function AlunosTreinadorPage() {
  const router = useRouter();
  const [statusFilter, setStatusFilter] = useState<VinculoStatus | "">("");

  const [confirmDesvincular, setConfirmDesvincular] = useState<VinculoDetalheResponse | null>(null);
  const [loadingDesvincular, setLoadingDesvincular] = useState(false);
  const [observacaoDesvincular, setObservacaoDesvincular] = useState("");

  const [aprovarDialog, setAprovarDialog] = useState<VinculoDetalheResponse | null>(null);
  const [pacotes, setPacotes] = useState<PacoteResponse[]>([]);
  const [selectedPacote, setSelectedPacote] = useState<PacoteResponse | null>(null);
  const [trarFichas, setTrarFichas] = useState(false);
  const [loadingAprovar, setLoadingAprovar] = useState(false);

  const [reativarDialog, setReativarDialog] = useState<VinculoDetalheResponse | null>(null);
  const [selectedPacoteReativar, setSelectedPacoteReativar] = useState<PacoteResponse | null>(null);
  const [loadingReativar, setLoadingReativar] = useState(false);

  const fetcher = useCallback(
    (p: number, ps: number) =>
      treinadorApi.listVinculos({ status: statusFilter || undefined, pagina: p + 1, tamanhoPagina: ps }).then((r) => r.data),
    [statusFilter]
  );
  const { items: vinculos, total, page, pageSize, loading, error, success, setPage, setPageSize, setError, setSuccess, reload } =
    usePaginatedList<VinculoDetalheResponse>({ fetcher, errorMessage: "Erro ao carregar alunos." });

  const loadPacotes = async () => {
    if (pacotes.length === 0) {
      try {
        const res = await treinadorApi.listPacotes();
        setPacotes(res.data);
      } catch (err) {
        setError(extractApiError(err, "Erro ao carregar pacotes."));
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
      reload();
    } catch (err) {
      setError(extractApiError(err, "Erro ao reativar aluno."));
    } finally {
      setLoadingReativar(false);
    }
  };

  const handleAprovar = async () => {
    if (!aprovarDialog || !selectedPacote) return;
    setLoadingAprovar(true);
    try {
      await treinadorApi.aprovarVinculo(aprovarDialog.vinculoId, selectedPacote.pacoteId, trarFichas);
      setSuccess(`${aprovarDialog.nomeAluno}: vínculo aprovado com o pacote "${selectedPacote.nome}".`);
      setAprovarDialog(null);
      reload();
    } catch (err) {
      setError(extractApiError(err, "Erro ao aprovar vínculo."));
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
      reload();
    } catch (err) {
      setError(extractApiError(err, "Erro ao desvincular aluno."));
    } finally {
      setLoadingDesvincular(false);
    }
  };

  return (
    <Box>
      <PageHeader title="Alunos" />

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!success} severity="success" message={success} onClose={() => setSuccess("")} />

      <Box sx={{ mb: 2 }}>
        <FormControl size="small" fullWidth sx={{ maxWidth: { sm: 220 } }}>
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

      <DataList
        loading={loading}
        items={vinculos}
        emptyMessage="Nenhum aluno encontrado."
        columns={COLUMNS}
        rowKey={(v) => v.vinculoId}
        onRowClick={(v) => router.push(`/treinador/alunos/${v.alunoId}`)}
        pagination={{ count: total, page, rowsPerPage: pageSize, onPageChange: setPage, onRowsPerPageChange: setPageSize }}
        renderCell={(v, i) => {
          if (i === 0) return (
            <>
              <Typography variant="body2" sx={{ fontWeight: 500 }}>{v.nomeAluno}</Typography>
              {v.emailAluno && (
                <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>{v.emailAluno}</Typography>
              )}
            </>
          );
          if (i === 1) return <StatusChip status={v.status} />;
          if (i === 2) return (
            <Typography variant="caption">{new Date(v.createdAt).toLocaleDateString("pt-BR")}</Typography>
          );
          return (
            <>
              {v.status === "AguardandoAprovacao" && (
                <>
                  <Tooltip title="Aprovar vínculo">
                    <IconButton size="small" color="success" onClick={(e) => { e.stopPropagation(); openAprovar(v); }}>
                      <CheckIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Reprovar vínculo">
                    <IconButton size="small" color="error" onClick={(e) => { e.stopPropagation(); setConfirmDesvincular(v); setObservacaoDesvincular(""); }}>
                      <CloseIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </>
              )}
              {v.status === "Ativo" && (
                <>
                  <Tooltip title="Ver detalhes">
                    <IconButton size="small" onClick={(e) => { e.stopPropagation(); router.push(`/treinador/alunos/${v.alunoId}`); }}>
                      <OpenInNewIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Desvincular">
                    <IconButton size="small" color="error" onClick={(e) => { e.stopPropagation(); setConfirmDesvincular(v); setObservacaoDesvincular(""); }}>
                      <LinkOffIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </>
              )}
              {v.status === "Inativo" && (
                <Tooltip title="Reativar aluno">
                  <IconButton size="small" color="primary" onClick={(e) => { e.stopPropagation(); openReativar(v); }}>
                    <ReplayIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              )}
            </>
          );
        }}
      />

      <Dialog open={!!aprovarDialog} onClose={() => setAprovarDialog(null)} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}>
        <DialogTitle>Aprovar — {aprovarDialog?.nomeAluno}</DialogTitle>
        <DialogContent sx={{ pt: 2 }}>
          <Autocomplete
            options={pacotes}
            getOptionLabel={(p) => p.descricao ? `${p.nome} — ${p.descricao}` : p.nome}
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

      <Dialog open={!!reativarDialog} onClose={() => setReativarDialog(null)} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}>
        <DialogTitle>Reativar — {reativarDialog?.nomeAluno}</DialogTitle>
        <DialogContent sx={{ pt: 2 }}>
          <Autocomplete
            options={pacotes}
            getOptionLabel={(p) => p.descricao ? `${p.nome} — ${p.descricao}` : p.nome}
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
