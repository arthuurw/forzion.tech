"use client";
import { useCallback, useEffect, useState } from "react";
import {
  Box, Select, MenuItem, FormControl, InputLabel, TextField,
  InputAdornment, IconButton, Typography, Chip, ToggleButtonGroup, ToggleButton,
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import { useRouter } from "next/navigation";
import PageHeader from "@/components/ui/PageHeader";
import AlertBanner from "@/components/ui/AlertBanner";
import DataList from "@/components/ui/DataList";
import type { Column } from "@/components/ui/ResponsiveTable";
import { leadsApi } from "@/lib/api/leads";
import { usePaginatedList } from "@/hooks/usePaginatedList";
import type { LeadListItem, LeadStatus, LeadSource, ObterMetricasLeadsResponse } from "@/types";
import {
  LEAD_STATUS_LABEL, LEAD_STATUS_COLOR, LEAD_SOURCE_LABEL, TIPO_CONTATO_LEAD_LABEL,
} from "@/lib/constants/labels";
import { formatarDataHora, periodoParaDatas } from "@/lib/utils/formatting";
import LeadsMetricasCharts from "./_charts/LeadsMetricasCharts";

const COLUMNS: Column[] = [
  { label: "Lead" },
  { label: "Origem" },
  { label: "Status" },
  { label: "Chegada" },
];

type Periodo = "7d" | "30d" | "60d" | "90d" | "6m" | "1a" | "tudo";

const PERIODOS: { value: Periodo; label: string }[] = [
  { value: "7d", label: "7 dias" },
  { value: "30d", label: "30 dias" },
  { value: "90d", label: "90 dias" },
  { value: "6m", label: "6 meses" },
  { value: "1a", label: "1 ano" },
  { value: "tudo", label: "Tudo" },
];

export default function LeadsTreinadorPage() {
  const router = useRouter();
  const [status, setStatus] = useState<LeadStatus | "">("");
  const [origem, setOrigem] = useState<LeadSource | "">("");
  const [inicio, setInicio] = useState("");
  const [fim, setFim] = useState("");
  const [termoInput, setTermoInput] = useState("");
  const [termo, setTermo] = useState("");

  const [periodo, setPeriodo] = useState<Periodo>("30d");
  const [metricas, setMetricas] = useState<ObterMetricasLeadsResponse | null>(null);
  const [metricasError, setMetricasError] = useState("");

  useEffect(() => {
    const { de, ate } = periodoParaDatas(periodo);
    leadsApi
      .metricas(de, ate)
      .then((res) => setMetricas(res.data))
      .catch(() => setMetricasError("Não foi possível carregar as métricas do período."));
  }, [periodo]);

  const fetcher = useCallback(
    (p: number, ps: number) =>
      leadsApi
        .listar({
          pagina: p + 1,
          tamanhoPagina: ps,
          status: status || undefined,
          origem: origem || undefined,
          inicio: inicio || undefined,
          fim: fim || undefined,
          termo: termo || undefined,
        })
        .then((r) => r.data),
    [status, origem, inicio, fim, termo]
  );

  const { items: leads, total, page, pageSize, loading, error, setPage, setPageSize, setError } =
    usePaginatedList<LeadListItem>({ fetcher, errorMessage: "Erro ao carregar leads." });

  const aplicarBusca = () => {
    setTermo(termoInput.trim());
    setPage(0);
  };

  return (
    <Box>
      <PageHeader
        title="Leads"
        subtitle="Interesses capturados pelo agente conversacional e cadastros manuais."
      />

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!metricasError} severity="warning" message={metricasError} onClose={() => setMetricasError("")} />

      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "flex-end", mb: 1 }}>
        <Box sx={{ overflowX: "auto", maxWidth: "100%", pb: 0.5 }}>
          <ToggleButtonGroup
            value={periodo}
            exclusive
            onChange={(_, val) => { if (val) setPeriodo(val); }}
            size="small"
            sx={{ flexWrap: "nowrap" }}
          >
            {PERIODOS.map((p) => (
              <ToggleButton key={p.value} value={p.value} sx={{ fontSize: "0.75rem", px: 1.5, whiteSpace: "nowrap" }}>
                {p.label}
              </ToggleButton>
            ))}
          </ToggleButtonGroup>
        </Box>
      </Box>

      {metricas && <LeadsMetricasCharts metricas={metricas} />}

      <Box sx={{ display: "flex", flexWrap: "wrap", gap: 2, mb: 2 }}>
        <FormControl size="small" sx={{ minWidth: 160 }}>
          <InputLabel id="lead-status-label">Status</InputLabel>
          <Select
            labelId="lead-status-label"
            value={status}
            label="Status"
            onChange={(e) => { setStatus(e.target.value as LeadStatus | ""); setPage(0); }}
          >
            <MenuItem value="">Todos</MenuItem>
            {(Object.keys(LEAD_STATUS_LABEL) as LeadStatus[]).map((s) => (
              <MenuItem key={s} value={s}>{LEAD_STATUS_LABEL[s]}</MenuItem>
            ))}
          </Select>
        </FormControl>

        <FormControl size="small" sx={{ minWidth: 160 }}>
          <InputLabel id="lead-origem-label">Origem</InputLabel>
          <Select
            labelId="lead-origem-label"
            value={origem}
            label="Origem"
            onChange={(e) => { setOrigem(e.target.value as LeadSource | ""); setPage(0); }}
          >
            <MenuItem value="">Todas</MenuItem>
            {(Object.keys(LEAD_SOURCE_LABEL) as LeadSource[]).map((s) => (
              <MenuItem key={s} value={s}>{LEAD_SOURCE_LABEL[s]}</MenuItem>
            ))}
          </Select>
        </FormControl>

        <TextField
          size="small"
          label="De"
          type="date"
          value={inicio}
          onChange={(e) => { setInicio(e.target.value); setPage(0); }}
          slotProps={{ inputLabel: { shrink: true } }}
        />
        <TextField
          size="small"
          label="Até"
          type="date"
          value={fim}
          onChange={(e) => { setFim(e.target.value); setPage(0); }}
          slotProps={{ inputLabel: { shrink: true } }}
        />

        <TextField
          size="small"
          label="Buscar por nome ou contato"
          value={termoInput}
          onChange={(e) => setTermoInput(e.target.value)}
          onKeyDown={(e) => { if (e.key === "Enter") aplicarBusca(); }}
          sx={{ minWidth: 220 }}
          slotProps={{
            input: {
              endAdornment: (
                <InputAdornment position="end">
                  <IconButton size="small" aria-label="Buscar leads" onClick={aplicarBusca}>
                    <SearchIcon fontSize="small" />
                  </IconButton>
                </InputAdornment>
              ),
            },
          }}
        />
      </Box>

      <DataList
        loading={loading}
        items={leads}
        emptyMessage="Nenhum lead ainda. Leads chegam pelo agente conversacional do seu perfil público, ou você pode cadastrar um lead manualmente."
        columns={COLUMNS}
        rowKey={(l) => l.id}
        onRowClick={(l) => router.push(`/treinador/leads/${l.id}`)}
        pagination={{ count: total, page, rowsPerPage: pageSize, onPageChange: setPage, onRowsPerPageChange: setPageSize }}
        renderCell={(l, i) => {
          if (i === 0) return (
            <>
              <Typography variant="body2" sx={{ fontWeight: 500 }}>{l.nome}</Typography>
              <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                {TIPO_CONTATO_LEAD_LABEL[l.contatoTipo]}: {l.contatoValor}
              </Typography>
            </>
          );
          if (i === 1) return LEAD_SOURCE_LABEL[l.source];
          if (i === 2) return <Chip label={LEAD_STATUS_LABEL[l.status]} color={LEAD_STATUS_COLOR[l.status]} size="small" />;
          return formatarDataHora(l.createdAt);
        }}
      />
    </Box>
  );
}
