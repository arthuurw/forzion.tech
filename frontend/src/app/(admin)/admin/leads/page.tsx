"use client";
import { useState } from "react";
import { Box, TextField, Button, InputAdornment, IconButton, Typography, Chip } from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import PageHeader from "@/components/ui/PageHeader";
import AlertBanner from "@/components/ui/AlertBanner";
import DataList from "@/components/ui/DataList";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import type { Column } from "@/components/ui/ResponsiveTable";
import { adminApi } from "@/lib/api/admin";
import { extractApiError } from "@/lib/api/extractApiError";
import type { LeadAdminItem } from "@/types";
import { LEAD_STATUS_LABEL, LEAD_STATUS_COLOR, TIPO_CONTATO_LEAD_LABEL } from "@/lib/constants/labels";
import { formatarDataHora } from "@/lib/utils/formatting";

const COLUMNS: Column[] = [
  { label: "Lead" },
  { label: "Treinador" },
  { label: "Status" },
  { label: "Criado em" },
  { label: "Ação", align: "right" },
];

export default function AdminLeadsPage() {
  const [contatoInput, setContatoInput] = useState("");
  const [buscou, setBuscou] = useState(false);
  const [resultados, setResultados] = useState<LeadAdminItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [anonimizarAlvo, setAnonimizarAlvo] = useState<LeadAdminItem | null>(null);
  const [anonimizando, setAnonimizando] = useState(false);

  const buscar = async () => {
    if (!contatoInput.trim()) return;
    setLoading(true);
    setError("");
    setBuscou(true);
    try {
      const res = await adminApi.buscarLeadsPorContato(contatoInput.trim());
      setResultados(res.data);
    } catch (err) {
      setError(extractApiError(err, "Erro ao buscar leads."));
    } finally {
      setLoading(false);
    }
  };

  const handleAnonimizar = async () => {
    if (!anonimizarAlvo) return;
    setAnonimizando(true);
    try {
      await adminApi.anonimizarLead(anonimizarAlvo.id);
      setSuccess(`Lead de "${anonimizarAlvo.nome}" anonimizado.`);
      setAnonimizarAlvo(null);
      await buscar();
    } catch (err) {
      setError(extractApiError(err, "Erro ao anonimizar lead."));
    } finally {
      setAnonimizando(false);
    }
  };

  return (
    <Box>
      <PageHeader
        title="Leads"
        subtitle="Atendimento ao titular sem conta (LGPD art. 18) — busque pelo contato para localizar e, se solicitado, anonimizar."
      />

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!success} severity="success" message={success} onClose={() => setSuccess("")} />

      <Box sx={{ display: "flex", gap: 2, mb: 2, flexWrap: "wrap" }}>
        <TextField
          size="small"
          label="Contato (e-mail ou telefone)"
          value={contatoInput}
          onChange={(e) => setContatoInput(e.target.value)}
          onKeyDown={(e) => { if (e.key === "Enter") buscar(); }}
          sx={{ minWidth: 260 }}
          slotProps={{
            input: {
              endAdornment: (
                <InputAdornment position="end">
                  <IconButton size="small" aria-label="Buscar leads por contato" onClick={buscar}>
                    <SearchIcon fontSize="small" />
                  </IconButton>
                </InputAdornment>
              ),
            },
          }}
        />
      </Box>

      {buscou && (
        <DataList
          loading={loading}
          items={resultados}
          emptyMessage="Nenhum lead encontrado para esse contato."
          columns={COLUMNS}
          rowKey={(l) => l.id}
          renderCell={(l, i) => {
            if (i === 0) return (
              <>
                <Typography variant="body2" sx={{ fontWeight: 500 }}>{l.nome}</Typography>
                <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                  {TIPO_CONTATO_LEAD_LABEL[l.contatoTipo]}: {l.contatoValor}
                </Typography>
              </>
            );
            if (i === 1) return (
              <Typography variant="caption" sx={{ fontFamily: "monospace", overflowWrap: "anywhere" }}>
                {l.treinadorId}
              </Typography>
            );
            if (i === 2) return <Chip label={LEAD_STATUS_LABEL[l.status]} color={LEAD_STATUS_COLOR[l.status]} size="small" />;
            if (i === 3) return formatarDataHora(l.createdAt);
            return (
              <Button
                variant="outlined"
                color="error"
                size="small"
                disabled={l.anonimizado}
                onClick={() => setAnonimizarAlvo(l)}
              >
                {l.anonimizado ? "Já anonimizado" : "Anonimizar"}
              </Button>
            );
          }}
        />
      )}

      <ConfirmDialog
        open={!!anonimizarAlvo}
        title="Anonimizar lead"
        description={`Anonimizar o lead de "${anonimizarAlvo?.nome}"? Nome, contato e interesse serão apagados permanentemente. Esta ação não pode ser desfeita.`}
        confirmLabel="Anonimizar"
        destructive
        loading={anonimizando}
        onConfirm={handleAnonimizar}
        onClose={() => setAnonimizarAlvo(null)}
      />
    </Box>
  );
}
