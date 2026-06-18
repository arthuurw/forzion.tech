"use client";
import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  Box, Typography, Card, CardContent, Stack, IconButton, Chip,
  Tab, Tabs, Tooltip, Button,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import InfoIcon from "@mui/icons-material/Info";
import StatusChip from "@/components/ui/StatusChip";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import DataList from "@/components/ui/DataList";
import InfoLine from "@/components/ui/InfoLine";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import type { Column } from "@/components/ui/ResponsiveTable";
import { adminApi } from "@/lib/api/admin";
import { extractApiError } from "@/lib/api/extractApiError";
import type {
  TreinadorResponse, AlunoResponse, VinculoDetalheResponse,
  TreinoResponse, PacoteResponse,
} from "@/types";
import { usePaginatedList } from "@/hooks/usePaginatedList";
import { OBJETIVO_LABEL } from "@/lib/constants/labels";

const ALUNOS_COLUMNS: Column[] = [
  { label: "Nome" },
  { label: "E-mail" },
  { label: "Status" },
];

const VINCULOS_COLUMNS: Column[] = [
  { label: "Aluno" },
  { label: "E-mail" },
  { label: "Status" },
  { label: "Desde" },
];

const TREINOS_COLUMNS: Column[] = [
  { label: "Nome" },
  { label: "Objetivo" },
  { label: "Criado em" },
  { label: "Ações", align: "right" },
];

const PACOTES_COLUMNS: Column[] = [
  { label: "Nome" },
  { label: "Preço" },
  { label: "Descrição" },
];

export default function DetalheTreinadorAdminPage() {
  const { treinadorId } = useParams<{ treinadorId: string }>();
  const router = useRouter();

  const [treinador, setTreinador] = useState<TreinadorResponse | null>(null);
  const [loadingHeader, setLoadingHeader] = useState(true);
  const [error, setError] = useState("");
  const [tab, setTab] = useState(0);

  const [pacotes, setPacotes] = useState<PacoteResponse[]>([]);
  const [pacotesLoading, setPacotesLoading] = useState(false);
  const [pacotesLoaded, setPacotesLoaded] = useState(false);

  const [exportingLgpd, setExportingLgpd] = useState(false);
  const [anonimizarDialog, setAnonimizarDialog] = useState(false);
  const [anonimizando, setAnonimizando] = useState(false);

  useEffect(() => {
    setLoadingHeader(true);
    adminApi.getTreinador(treinadorId)
      .then((res) => setTreinador(res.data))
      .catch((err) => setError(extractApiError(err, "Erro ao carregar dados do treinador.")))
      .finally(() => setLoadingHeader(false));
  }, [treinadorId]);

  const alunosFetcher = useCallback(
    (p: number, ps: number) =>
      adminApi.getTreinadorAlunos(treinadorId, { pagina: p + 1, tamanhoPagina: ps }).then((r) => r.data),
    [treinadorId]
  );

  const vinculosFetcher = useCallback(
    (p: number, ps: number) =>
      adminApi.getTreinadorVinculos(treinadorId, { pagina: p + 1, tamanhoPagina: ps }).then((r) => r.data),
    [treinadorId]
  );

  const treinosFetcher = useCallback(
    (p: number, ps: number) =>
      adminApi.getTreinadorTreinos(treinadorId, { pagina: p + 1, tamanhoPagina: ps }).then((r) => r.data),
    [treinadorId]
  );

  const alunosList = usePaginatedList<AlunoResponse>({ fetcher: alunosFetcher, errorMessage: "Erro ao carregar alunos." });
  const vinculosList = usePaginatedList<VinculoDetalheResponse>({ fetcher: vinculosFetcher, errorMessage: "Erro ao carregar vínculos." });
  const treinosList = usePaginatedList<TreinoResponse>({ fetcher: treinosFetcher, errorMessage: "Erro ao carregar treinos." });

  useEffect(() => {
    if (tab === 3 && !pacotesLoaded) {
      setPacotesLoading(true);
      adminApi.getTreinadorPacotes(treinadorId)
        .then((res) => setPacotes(res.data))
        .catch((err) => setError(extractApiError(err, "Erro ao carregar pacotes.")))
        .finally(() => { setPacotesLoading(false); setPacotesLoaded(true); });
    }
  }, [tab, treinadorId, pacotesLoaded]);

  const handleExportarLgpd = async () => {
    if (!treinador?.contaId) return;
    setExportingLgpd(true);
    try {
      const res = await adminApi.exportarDadosConta(treinador.contaId);
      const url = URL.createObjectURL(res.data as Blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `dados-${treinador.contaId}.json`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      setError(extractApiError(err, "Erro ao exportar dados."));
    } finally {
      setExportingLgpd(false);
    }
  };

  const handleAnonimizar = async () => {
    if (!treinador?.contaId) return;
    setAnonimizando(true);
    try {
      await adminApi.anonimizarConta(treinador.contaId);
      setAnonimizarDialog(false);
      router.push("/admin/treinadores");
    } catch (err) {
      setError(extractApiError(err, "Erro ao anonimizar conta."));
      setAnonimizarDialog(false);
    } finally {
      setAnonimizando(false);
    }
  };

  if (loadingHeader) return <LoadingSpinner />;

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 2 }}>
        <IconButton onClick={() => router.push("/admin/treinadores")} aria-label="Voltar">
          <ArrowBackIcon />
        </IconButton>
        <Box sx={{ flex: 1 }}>
          <Typography variant="h5" sx={{ fontWeight: 700 }}>
            {treinador?.nome ?? "Treinador"}
          </Typography>
          {treinador && (
            <Typography variant="caption" color="text.secondary">
              Cadastro: {new Date(treinador.createdAt).toLocaleDateString("pt-BR")}
            </Typography>
          )}
        </Box>
        {treinador && <StatusChip status={treinador.status} />}
      </Box>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      <Tabs value={tab} onChange={(_, v) => setTab(v)} variant="scrollable" scrollButtons="auto" allowScrollButtonsMobile sx={{ mb: 3, borderBottom: 1, borderColor: "divider" }}>
        <Tab label="Alunos" />
        <Tab label="Vínculos" />
        <Tab label="Treinos" />
        <Tab label="Pacotes" />
        <Tab label="LGPD" />
      </Tabs>

      {/* Tab 0: Alunos */}
      {tab === 0 && (
        <DataList
          loading={alunosList.loading}
          items={alunosList.items}
          emptyMessage="Este treinador não possui alunos ativos."
          columns={ALUNOS_COLUMNS}
          rowKey={(a) => a.alunoId}
          pagination={{
            count: alunosList.total,
            page: alunosList.page,
            rowsPerPage: alunosList.pageSize,
            onPageChange: alunosList.setPage,
            onRowsPerPageChange: alunosList.setPageSize,
          }}
          renderCell={(a, i) => {
            if (i === 0) return (
              <Typography
                variant="body2"
                sx={{ fontWeight: 500, cursor: "pointer", "&:hover": { color: "primary.main" } }}
                onClick={() => router.push(`/admin/alunos/${a.alunoId}`)}
              >
                {a.nome}
              </Typography>
            );
            if (i === 1) return <Typography variant="body2" color="text.secondary">{a.email ?? "—"}</Typography>;
            return <StatusChip status={a.status} />;
          }}
        />
      )}

      {/* Tab 1: Vínculos */}
      {tab === 1 && (
        <DataList
          loading={vinculosList.loading}
          items={vinculosList.items}
          emptyMessage="Nenhum vínculo encontrado."
          columns={VINCULOS_COLUMNS}
          rowKey={(v) => v.vinculoId}
          pagination={{
            count: vinculosList.total,
            page: vinculosList.page,
            rowsPerPage: vinculosList.pageSize,
            onPageChange: vinculosList.setPage,
            onRowsPerPageChange: vinculosList.setPageSize,
          }}
          renderCell={(v, i) => {
            if (i === 0) return (
              <Typography
                variant="body2"
                sx={{ fontWeight: 500, cursor: "pointer", "&:hover": { color: "primary.main" } }}
                onClick={() => router.push(`/admin/alunos/${v.alunoId}`)}
              >
                {v.nomeAluno}
              </Typography>
            );
            if (i === 1) return <Typography variant="body2" color="text.secondary">{v.emailAluno ?? "—"}</Typography>;
            if (i === 2) return <StatusChip status={v.status} />;
            return (
              <Typography variant="caption">
                {new Date(v.createdAt).toLocaleDateString("pt-BR")}
              </Typography>
            );
          }}
        />
      )}

      {/* Tab 2: Treinos */}
      {tab === 2 && (
        <DataList
          loading={treinosList.loading}
          items={treinosList.items}
          emptyMessage="Nenhum treino encontrado para este treinador."
          columns={TREINOS_COLUMNS}
          rowKey={(t) => t.treinoId}
          pagination={{
            count: treinosList.total,
            page: treinosList.page,
            rowsPerPage: treinosList.pageSize,
            onPageChange: treinosList.setPage,
            onRowsPerPageChange: treinosList.setPageSize,
          }}
          renderCell={(t, i) => {
            if (i === 0) return <Typography variant="body2" sx={{ fontWeight: 500 }}>{t.nome}</Typography>;
            if (i === 1) return <Typography variant="body2">{OBJETIVO_LABEL[t.objetivo] ?? t.objetivo}</Typography>;
            if (i === 2) return (
              <Typography variant="caption">{new Date(t.createdAt).toLocaleDateString("pt-BR")}</Typography>
            );
            return (
              <Tooltip title="Ver detalhe">
                <IconButton size="small" aria-label="Ver detalhe do treino" onClick={() => router.push(`/admin/treinos/${t.treinoId}`)}>
                  <InfoIcon fontSize="small" />
                </IconButton>
              </Tooltip>
            );
          }}
        />
      )}

      {/* Tab 3: Pacotes */}
      {tab === 3 && (
        pacotesLoading ? (
          <LoadingSpinner />
        ) : pacotes.length === 0 ? (
          <Card variant="outlined">
            <CardContent sx={{ textAlign: "center", py: 3 }}>
              <Typography color="text.secondary">Nenhum pacote cadastrado.</Typography>
            </CardContent>
          </Card>
        ) : (
          <DataList
            loading={false}
            items={pacotes}
            emptyMessage=""
            columns={PACOTES_COLUMNS}
            rowKey={(p) => p.pacoteId}
            renderCell={(p, i) => {
              if (i === 0) return (
                <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                  <Typography variant="body2" sx={{ fontWeight: 500 }}>{p.nome}</Typography>
                  {p.isAtivo === false && <Chip label="Inativo" size="small" color="default" />}
                </Stack>
              );
              if (i === 1) return (
                <Typography variant="body2">
                  {p.preco.toLocaleString("pt-BR", { style: "currency", currency: "BRL" })}
                </Typography>
              );
              return (
                <Typography variant="body2" color="text.secondary" sx={{ maxWidth: { xs: 200, md: 300 }, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
                  {p.descricao ?? "—"}
                </Typography>
              );
            }}
          />
        )
      )}
      {/* Tab 4: LGPD */}
      {tab === 4 && (
        <Card variant="outlined">
          <CardContent>
            <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 2 }}>
              Ações LGPD
            </Typography>
            <Stack spacing={1.5}>
              <Button
                variant="outlined"
                size="small"
                disabled={exportingLgpd || !treinador?.contaId}
                onClick={handleExportarLgpd}
                sx={{ alignSelf: "flex-start" }}
              >
                {exportingLgpd ? "Exportando..." : "Exportar dados (LGPD)"}
              </Button>
              <Button
                variant="outlined"
                color="error"
                size="small"
                disabled={!treinador?.contaId}
                onClick={() => setAnonimizarDialog(true)}
                sx={{ alignSelf: "flex-start" }}
              >
                Anonimizar conta (LGPD)
              </Button>
            </Stack>
          </CardContent>
        </Card>
      )}

      <ConfirmDialog
        open={anonimizarDialog}
        title="Anonimizar conta"
        description="Esta ação é irreversível. Os dados pessoais desta conta serão anonimizados permanentemente (LGPD - direito ao esquecimento)."
        confirmLabel={anonimizando ? "Anonimizando..." : "Anonimizar"}
        destructive
        loading={anonimizando}
        onConfirm={handleAnonimizar}
        onClose={() => setAnonimizarDialog(false)}
      />
    </Box>
  );
}
