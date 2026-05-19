"use client";
import { useState, useCallback } from "react";
import { useRouter } from "next/navigation";
import {
  Box, Typography, Select, MenuItem, FormControl,
  InputLabel, IconButton, Tooltip, Dialog, DialogTitle,
  DialogContent, DialogActions, Button, Autocomplete, TextField,
} from "@mui/material";
import CheckIcon from "@mui/icons-material/Check";
import CloseIcon from "@mui/icons-material/Close";
import BlockIcon from "@mui/icons-material/Block";
import CardMembershipIcon from "@mui/icons-material/CardMembership";
import DeleteForeverIcon from "@mui/icons-material/DeleteForever";
import InfoIcon from "@mui/icons-material/Info";
import StatusChip from "@/components/ui/StatusChip";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import AlertBanner from "@/components/ui/AlertBanner";
import DataList from "@/components/ui/DataList";
import type { Column } from "@/components/ui/ResponsiveTable";
import { adminApi } from "@/lib/api/admin";
import type { TreinadorResponse, TreinadorStatus, PlanoTreinadorResponse } from "@/types";
import { usePaginatedList } from "@/hooks/usePaginatedList";

const COLUMNS: Column[] = [
  { label: "Nome" },
  { label: "Status" },
  { label: "Plano" },
  { label: "Cadastro" },
  { label: "Ações", align: "right" },
];



export default function TreinadoresAdminPage() {
  const router = useRouter();
  const [statusFilter, setStatusFilter] = useState<TreinadorStatus | "">("");

  const [confirmAprovar, setConfirmAprovar] = useState<TreinadorResponse | null>(null);
  const [loadingAprovar, setLoadingAprovar] = useState(false);
  const [observacaoAprovar, setObservacaoAprovar] = useState("");

  const [confirmReprovar, setConfirmReprovar] = useState<TreinadorResponse | null>(null);
  const [loadingReprovar, setLoadingReprovar] = useState(false);
  const [observacaoReprovar, setObservacaoReprovar] = useState("");

  const [confirmInativar, setConfirmInativar] = useState<TreinadorResponse | null>(null);
  const [loadingInativar, setLoadingInativar] = useState(false);
  const [observacaoInativar, setObservacaoInativar] = useState("");

  const [confirmExcluir, setConfirmExcluir] = useState<TreinadorResponse | null>(null);
  const [loadingExcluir, setLoadingExcluir] = useState(false);

  const [planoDialog, setPlanoDialog] = useState<TreinadorResponse | null>(null);
  const [planos, setPlanos] = useState<PlanoTreinadorResponse[]>([]);
  const [selectedPlano, setSelectedPlano] = useState<PlanoTreinadorResponse | null>(null);
  const [loadingPlano, setLoadingPlano] = useState(false);

  const fetcher = useCallback(
    (p: number, ps: number) =>
      adminApi.listTreinadores({ status: statusFilter || undefined, pagina: p + 1, tamanhoPagina: ps }).then((r) => r.data),
    [statusFilter]
  );
  const { items: treinadores, total, page, pageSize, loading, error, success, setPage, setPageSize, setError, setSuccess, reload } =
    usePaginatedList<TreinadorResponse>({ fetcher, errorMessage: "Erro ao carregar treinadores." });

  const handleAprovar = async () => {
    if (!confirmAprovar) return;
    setLoadingAprovar(true);
    try {
      await adminApi.aprovarTreinador(confirmAprovar.treinadorId, observacaoAprovar.trim() || null);
      setSuccess(`${confirmAprovar.nome} aprovado com sucesso.`);
      setConfirmAprovar(null);
      setObservacaoAprovar("");
      reload();
    } catch {
      setError("Erro ao aprovar treinador.");
    } finally {
      setLoadingAprovar(false);
    }
  };

  const handleReprovar = async () => {
    if (!confirmReprovar) return;
    setLoadingReprovar(true);
    try {
      await adminApi.reprovarTreinador(confirmReprovar.treinadorId, observacaoReprovar.trim() || null);
      setSuccess(`${confirmReprovar.nome} reprovado.`);
      setConfirmReprovar(null);
      setObservacaoReprovar("");
      reload();
    } catch {
      setError("Erro ao reprovar treinador.");
    } finally {
      setLoadingReprovar(false);
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
      reload();
    } catch {
      setError("Erro ao inativar treinador.");
    } finally {
      setLoadingInativar(false);
    }
  };

  const handleExcluir = async () => {
    if (!confirmExcluir) return;
    setLoadingExcluir(true);
    try {
      await adminApi.excluirTreinador(confirmExcluir.treinadorId);
      setSuccess(`${confirmExcluir.nome} excluído permanentemente.`);
      setConfirmExcluir(null);
      reload();
    } catch {
      setError("Erro ao excluir treinador.");
    } finally {
      setLoadingExcluir(false);
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
      reload();
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
        <FormControl size="small" fullWidth sx={{ maxWidth: { sm: 220 } }}>
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

      <DataList
        loading={loading}
        items={treinadores}
        emptyMessage="Nenhum treinador encontrado para os filtros aplicados."
        columns={COLUMNS}
        rowKey={(t) => t.treinadorId}
        pagination={{ count: total, page, rowsPerPage: pageSize, onPageChange: setPage, onRowsPerPageChange: setPageSize }}
        renderCell={(t, i) => {
          if (i === 0) return t.nome;
          if (i === 1) return <StatusChip status={t.status} />;
          if (i === 2) return t.planoTreinadorId ? (
            <Typography variant="caption" color="text.secondary">Atribuído</Typography>
          ) : (
            <Typography variant="caption" color="text.disabled">-</Typography>
          );
          if (i === 3) return (
            <Typography variant="caption">{new Date(t.createdAt).toLocaleDateString("pt-BR")}</Typography>
          );
          return (
            <>
              {t.status === "AguardandoAprovacao" && (
                <>
                  <Tooltip title="Aprovar">
                    <IconButton size="small" color="success" onClick={() => setConfirmAprovar(t)}>
                      <CheckIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Reprovar">
                    <IconButton size="small" color="error" onClick={() => setConfirmReprovar(t)}>
                      <CloseIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </>
              )}
              {t.status === "Ativo" && (
                <Tooltip title="Inativar">
                  <IconButton size="small" color="error" onClick={() => setConfirmInativar(t)}>
                    <BlockIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              )}
              {t.status === "Inativo" && (
                <Tooltip title="Excluir permanentemente">
                  <IconButton size="small" color="error" onClick={() => setConfirmExcluir(t)}>
                    <DeleteForeverIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              )}
              <Tooltip title="Atribuir plano">
                <IconButton size="small" onClick={() => openPlanoDialog(t)}>
                  <CardMembershipIcon fontSize="small" />
                </IconButton>
              </Tooltip>
              <Tooltip title="Ver detalhe">
                <IconButton size="small" onClick={() => router.push(`/admin/treinadores/${t.treinadorId}`)}>
                  <InfoIcon fontSize="small" />
                </IconButton>
              </Tooltip>
            </>
          );
        }}
      />

      <ConfirmDialog
        open={!!confirmAprovar}
        title="Confirmar aprovação de acesso"
        description={`Liberar acesso à plataforma para "${confirmAprovar?.nome}"?`}
        confirmLabel="Aprovar"
        loading={loadingAprovar}
        onConfirm={handleAprovar}
        onClose={() => { setConfirmAprovar(null); setObservacaoAprovar(""); }}
      >
        <TextField
          label="Observação (opcional)"
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
        open={!!confirmReprovar}
        title="Reprovar treinador"
        description={`Reprovar "${confirmReprovar?.nome}" negará o acesso à plataforma. O cadastro ficará inativo.`}
        confirmLabel="Reprovar"
        destructive
        loading={loadingReprovar}
        onConfirm={handleReprovar}
        onClose={() => { setConfirmReprovar(null); setObservacaoReprovar(""); }}
      >
        <TextField
          label="Motivo (opcional)"
          value={observacaoReprovar}
          onChange={(e) => setObservacaoReprovar(e.target.value)}
          size="small"
          fullWidth
          multiline
          rows={3}
          sx={{ mt: 2 }}
        />
      </ConfirmDialog>

      <ConfirmDialog
        open={!!confirmInativar}
        title="Desativar treinador"
        description={`Desativar "${confirmInativar?.nome}" encerrará todos os vínculos com alunos e desativará as fichas associadas. Esta ação não pode ser desfeita.`}
        confirmLabel="Desativar"
        destructive
        loading={loadingInativar}
        onConfirm={handleInativar}
        onClose={() => { setConfirmInativar(null); setObservacaoInativar(""); }}
      >
        <TextField
          label="Observação (opcional)"
          value={observacaoInativar}
          onChange={(e) => setObservacaoInativar(e.target.value)}
          size="small"
          fullWidth
          multiline
          rows={3}
          sx={{ mt: 2 }}
        />
      </ConfirmDialog>

      <ConfirmDialog
        open={!!confirmExcluir}
        title="Excluir treinador permanentemente"
        description={`Esta ação removerá "${confirmExcluir?.nome}" e todos os seus dados (treinos, fichas, vínculos, pacotes e conta). O histórico de aprovações é preservado. Não pode ser desfeita.`}
        confirmLabel="Excluir"
        destructive
        loading={loadingExcluir}
        onConfirm={handleExcluir}
        onClose={() => setConfirmExcluir(null)}
      />

      <Dialog open={!!planoDialog} onClose={() => setPlanoDialog(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Atribuir plano — {planoDialog?.nome}</DialogTitle>
        <DialogContent sx={{ pt: 2 }}>
          {planoDialog?.planoTreinadorId && (
            <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 1.5 }}>
              Plano atual:{" "}
              <strong>
                {planos.find((p) => p.planoId === planoDialog.planoTreinadorId)?.nome ?? "carregando..."}
              </strong>
            </Typography>
          )}
          <Autocomplete
            options={planos}
            getOptionLabel={(p) => `${p.nome} (até ${p.maxAlunos} alunos)`}
            value={selectedPlano}
            onChange={(_, v) => setSelectedPlano(v)}
            renderInput={(params) => <TextField {...params} label="Novo plano" size="small" />}
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
