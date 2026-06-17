"use client";
import { useState, useCallback, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import {
  Box, Typography, Select, MenuItem, FormControl,
  InputLabel, IconButton, Tooltip, Dialog, DialogTitle,
  DialogContent, DialogActions, Button, Autocomplete, TextField,
  Menu, ListItemIcon, ListItemText, useMediaQuery, useTheme,
} from "@mui/material";
import CheckIcon from "@mui/icons-material/Check";
import CloseIcon from "@mui/icons-material/Close";
import BlockIcon from "@mui/icons-material/Block";
import CardMembershipIcon from "@mui/icons-material/CardMembership";
import DeleteForeverIcon from "@mui/icons-material/DeleteForever";
import InfoIcon from "@mui/icons-material/Info";
import MoreVertIcon from "@mui/icons-material/MoreVert";
import StatusChip from "@/components/ui/StatusChip";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import AlertBanner from "@/components/ui/AlertBanner";
import DataList from "@/components/ui/DataList";
import type { Column } from "@/components/ui/ResponsiveTable";
import { adminApi } from "@/lib/api/admin";
import type { TreinadorResponse, TreinadorStatus, PlanoPlataformaResponse } from "@/types";
import { usePaginatedList } from "@/hooks/usePaginatedList";

const COLUMNS: Column[] = [
  { label: "Nome" },
  { label: "Status" },
  { label: "Plano" },
  { label: "Cadastro" },
  { label: "Ações", align: "right" },
];

interface AcoesProps {
  t: TreinadorResponse;
  isMobile: boolean;
  onAprovar: (t: TreinadorResponse) => void;
  onReprovar: (t: TreinadorResponse) => void;
  onInativar: (t: TreinadorResponse) => void;
  onExcluir: (t: TreinadorResponse) => void;
  onAtribuirPlano: (t: TreinadorResponse) => void;
  onDetalhe: (t: TreinadorResponse) => void;
}

// <md o slot de ações vira kebab (até 4 IconButtons espremiam o nome no card — R5).
// ≥md mantém os IconButtons inline.
function TreinadorAcoes({
  t, isMobile, onAprovar, onReprovar, onInativar, onExcluir, onAtribuirPlano, onDetalhe,
}: AcoesProps) {
  const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
  const close = () => setAnchorEl(null);

  if (!isMobile) {
    return (
      <>
        {t.status === "AguardandoAprovacao" && (
          <>
            <Tooltip title="Aprovar">
              <IconButton size="small" color="success" aria-label="Aprovar treinador" onClick={() => onAprovar(t)}>
                <CheckIcon fontSize="small" />
              </IconButton>
            </Tooltip>
            <Tooltip title="Reprovar">
              <IconButton size="small" color="error" aria-label="Reprovar treinador" onClick={() => onReprovar(t)}>
                <CloseIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          </>
        )}
        {t.status === "Ativo" && (
          <Tooltip title="Inativar">
            <IconButton size="small" color="error" aria-label="Inativar treinador" onClick={() => onInativar(t)}>
              <BlockIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        )}
        {t.status === "Inativo" && (
          <Tooltip title="Excluir permanentemente">
            <IconButton size="small" color="error" aria-label="Excluir treinador permanentemente" onClick={() => onExcluir(t)}>
              <DeleteForeverIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        )}
        <Tooltip title="Atribuir plano">
          <IconButton size="small" aria-label="Atribuir plano" onClick={() => onAtribuirPlano(t)}>
            <CardMembershipIcon fontSize="small" />
          </IconButton>
        </Tooltip>
        <Tooltip title="Ver detalhe">
          <IconButton size="small" aria-label="Ver detalhe do treinador" onClick={() => onDetalhe(t)}>
            <InfoIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </>
    );
  }

  const itens: { label: string; icon: ReactNode; onClick: () => void }[] = [];
  if (t.status === "AguardandoAprovacao") {
    itens.push({ label: "Aprovar", icon: <CheckIcon fontSize="small" color="success" />, onClick: () => onAprovar(t) });
    itens.push({ label: "Reprovar", icon: <CloseIcon fontSize="small" color="error" />, onClick: () => onReprovar(t) });
  }
  if (t.status === "Ativo") {
    itens.push({ label: "Inativar", icon: <BlockIcon fontSize="small" color="error" />, onClick: () => onInativar(t) });
  }
  if (t.status === "Inativo") {
    itens.push({ label: "Excluir permanentemente", icon: <DeleteForeverIcon fontSize="small" color="error" />, onClick: () => onExcluir(t) });
  }
  itens.push({ label: "Atribuir plano", icon: <CardMembershipIcon fontSize="small" />, onClick: () => onAtribuirPlano(t) });
  itens.push({ label: "Ver detalhe", icon: <InfoIcon fontSize="small" />, onClick: () => onDetalhe(t) });

  return (
    <>
      <IconButton size="small" aria-label={`Ações de ${t.nome}`} onClick={(e) => setAnchorEl(e.currentTarget)}>
        <MoreVertIcon fontSize="small" />
      </IconButton>
      <Menu anchorEl={anchorEl} open={Boolean(anchorEl)} onClose={close}>
        {itens.map((it) => (
          <MenuItem key={it.label} onClick={() => { close(); it.onClick(); }}>
            <ListItemIcon>{it.icon}</ListItemIcon>
            <ListItemText>{it.label}</ListItemText>
          </MenuItem>
        ))}
      </Menu>
    </>
  );
}

export default function TreinadoresAdminPage() {
  const router = useRouter();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));
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
  const [planos, setPlanos] = useState<PlanoPlataformaResponse[]>([]);
  const [selectedPlano, setSelectedPlano] = useState<PlanoPlataformaResponse | null>(null);
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
      setSuccess(`${confirmAprovar.nome}: cadastro aprovado.`);
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
      setSuccess(`${confirmReprovar.nome}: cadastro recusado.`);
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
      setSuccess(`${confirmInativar.nome}: conta inativada. Vínculos e fichas afetados.`);
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
      setSuccess(`${confirmExcluir.nome}: conta excluída permanentemente.`);
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
          if (i === 2) return t.planoPlataformaId ? (
            <Typography variant="caption" color="text.secondary">Atribuído</Typography>
          ) : (
            <Typography variant="caption" color="text.disabled">-</Typography>
          );
          if (i === 3) return (
            <Typography variant="caption">{new Date(t.createdAt).toLocaleDateString("pt-BR")}</Typography>
          );
          return (
            <TreinadorAcoes
              t={t}
              isMobile={isMobile}
              onAprovar={setConfirmAprovar}
              onReprovar={setConfirmReprovar}
              onInativar={setConfirmInativar}
              onExcluir={setConfirmExcluir}
              onAtribuirPlano={openPlanoDialog}
              onDetalhe={(tr) => router.push(`/admin/treinadores/${tr.treinadorId}`)}
            />
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

      <Dialog open={!!planoDialog} onClose={() => setPlanoDialog(null)} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}>
        <DialogTitle>Atribuir plano — {planoDialog?.nome}</DialogTitle>
        <DialogContent sx={{ pt: 2 }}>
          {planoDialog?.planoPlataformaId && (
            <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 1.5 }}>
              Plano atual:{" "}
              <strong>
                {planos.find((p) => p.planoId === planoDialog.planoPlataformaId)?.nome ?? "carregando..."}
              </strong>
            </Typography>
          )}
          <Autocomplete
            options={planos.filter((p) => p.tier !== "Elite")}
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
