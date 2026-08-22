"use client";
import { useCallback, useEffect, useState } from "react";
import { useParams } from "next/navigation";
import {
  Box, Card, CardContent, Stack, Typography, Button, TextField,
  FormControl, InputLabel, Select, MenuItem, FormHelperText, Divider, Chip,
} from "@mui/material";
import PageHeader from "@/components/ui/PageHeader";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import DetalheErro from "@/components/ui/DetalheErro";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import InfoLine from "@/components/ui/InfoLine";
import { leadsApi } from "@/lib/api/leads";
import { extractApiError } from "@/lib/api/extractApiError";
import type { LeadDetalheResponse, MotivoDescarteLead } from "@/types";
import {
  LEAD_STATUS_LABEL, LEAD_STATUS_COLOR, LEAD_SOURCE_LABEL,
  TIPO_CONTATO_LEAD_LABEL, MOTIVO_DESCARTE_LEAD_LABEL,
} from "@/lib/constants/labels";
import { formatarDataHora } from "@/lib/utils/formatting";

export default function DetalheLeadPage() {
  const { id } = useParams<{ id: string }>();
  const [lead, setLead] = useState<LeadDetalheResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [acaoLoading, setAcaoLoading] = useState(false);
  const [observacaoInteracao, setObservacaoInteracao] = useState("");

  const [descartarOpen, setDescartarOpen] = useState(false);
  const [motivoDescarte, setMotivoDescarte] = useState<MotivoDescarteLead | "">("");
  const [observacaoDescarte, setObservacaoDescarte] = useState("");
  const [motivoErro, setMotivoErro] = useState("");

  const [convidarOpen, setConvidarOpen] = useState(false);
  const [convidando, setConvidando] = useState(false);
  const [conviteEnviado, setConviteEnviado] = useState(false);
  const [conviteAviso, setConviteAviso] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const res = await leadsApi.obter(id);
      setLead(res.data);
    } catch (err) {
      setError(extractApiError(err, "Erro ao carregar o lead."));
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => { load(); }, [load]);

  const isTerminal = lead?.status === "Convertido" || lead?.status === "Descartado";
  const acoesBloqueadas = !lead || isTerminal || lead.anonimizado;

  const handleMarcarEmContato = async () => {
    if (!lead) return;
    setAcaoLoading(true);
    try {
      await leadsApi.atualizarStatus(lead.id, { novoStatus: "EmContato" });
      setSuccess("Lead marcado como em contato.");
      await load();
    } catch (err) {
      setError(extractApiError(err, "Erro ao atualizar o lead."));
    } finally {
      setAcaoLoading(false);
    }
  };

  const handleRegistrarInteracao = async () => {
    if (!lead || !observacaoInteracao.trim()) return;
    setAcaoLoading(true);
    try {
      await leadsApi.registrarInteracao(lead.id, observacaoInteracao.trim());
      setSuccess("Interação registrada.");
      setObservacaoInteracao("");
      await load();
    } catch (err) {
      setError(extractApiError(err, "Erro ao registrar interação."));
    } finally {
      setAcaoLoading(false);
    }
  };

  const abrirDescartar = () => {
    setMotivoDescarte("");
    setObservacaoDescarte("");
    setMotivoErro("");
    setDescartarOpen(true);
  };

  const handleDescartar = async () => {
    if (!motivoDescarte) {
      setMotivoErro("Selecione um motivo para descartar o lead.");
      return;
    }
    if (!lead) return;
    setAcaoLoading(true);
    try {
      await leadsApi.atualizarStatus(lead.id, {
        novoStatus: "Descartado",
        motivo: motivoDescarte,
        observacao: observacaoDescarte.trim() || null,
      });
      setSuccess("Lead descartado.");
      setDescartarOpen(false);
      await load();
    } catch (err) {
      setError(extractApiError(err, "Erro ao descartar o lead."));
    } finally {
      setAcaoLoading(false);
    }
  };

  const handleConvidar = async () => {
    if (!lead) return;
    setConvidando(true);
    setConviteAviso("");
    try {
      const res = await leadsApi.enviarConvite(lead.id);
      setConvidarOpen(false);
      setConviteEnviado(true);
      if (res.data.enviado) {
        setSuccess("Convite enviado.");
      } else {
        setConviteAviso("Convite gerado, mas o e-mail não saiu — tente reenviar.");
      }
    } catch (err) {
      setError(extractApiError(err, "Erro ao enviar convite."));
    } finally {
      setConvidando(false);
    }
  };

  if (loading) return <LoadingSpinner />;
  if (!lead) return <DetalheErro mensagem={error || "Lead não encontrado."} onRetry={load} />;

  return (
    <Box>
      <PageHeader
        title={lead.nome}
        backHref="/treinador/leads"
        action={<Chip label={LEAD_STATUS_LABEL[lead.status]} color={LEAD_STATUS_COLOR[lead.status]} />}
      />

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!success} severity="success" message={success} onClose={() => setSuccess("")} />
      <AlertBanner open={!!conviteAviso} severity="warning" message={conviteAviso} onClose={() => setConviteAviso("")} />

      {lead.anonimizado && (
        <AlertBanner
          open
          severity="warning"
          message="Este lead foi anonimizado por política de retenção. Nenhuma ação está disponível."
        />
      )}
      {!lead.anonimizado && isTerminal && (
        <AlertBanner
          open
          severity="info"
          message={`Este lead já foi finalizado (${LEAD_STATUS_LABEL[lead.status]}). Nenhuma ação está disponível.`}
        />
      )}

      <Card variant="outlined" sx={{ mb: 3 }}>
        <CardContent>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ flexWrap: "wrap", mb: 1 }}>
            <InfoLine label={TIPO_CONTATO_LEAD_LABEL[lead.contatoTipo]} value={lead.contatoValor} />
            <InfoLine label="Origem" value={LEAD_SOURCE_LABEL[lead.source]} />
            <InfoLine label="Chegou em" value={formatarDataHora(lead.createdAt)} />
          </Stack>
          {lead.interesse && <InfoLine label="Interesse" value={lead.interesse} />}
          <Divider sx={{ my: 1.5 }} />
          <InfoLine label="Consentimento" value={lead.consentimentoFinalidade} />
          <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 0.5 }}>
            Concedido em {formatarDataHora(lead.consentimentoConcedidoEm)}
          </Typography>
        </CardContent>
      </Card>

      {!acoesBloqueadas && (
        <Card variant="outlined" sx={{ mb: 3 }}>
          <CardContent>
            <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5 }}>Ações</Typography>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ mb: 2 }}>
              <Button
                variant="outlined"
                disabled={acaoLoading || lead.status !== "Novo"}
                onClick={handleMarcarEmContato}
              >
                Marcar em contato
              </Button>
              <Button variant="outlined" color="error" disabled={acaoLoading} onClick={abrirDescartar}>
                Descartar
              </Button>
              <Button
                variant="outlined"
                disabled={acaoLoading || lead.contatoTipo === "Telefone"}
                onClick={() => setConvidarOpen(true)}
              >
                {conviteEnviado ? "Reenviar convite" : "Enviar convite"}
              </Button>
            </Stack>
            {lead.contatoTipo === "Telefone" && (
              <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 2 }}>
                Convite exige contato por e-mail ou WhatsApp.
              </Typography>
            )}
            <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5}>
              <TextField
                size="small"
                fullWidth
                label="Registrar interação"
                value={observacaoInteracao}
                onChange={(e) => setObservacaoInteracao(e.target.value)}
                multiline
                minRows={2}
              />
              <Button
                variant="contained"
                disabled={acaoLoading || !observacaoInteracao.trim()}
                onClick={handleRegistrarInteracao}
              >
                Registrar
              </Button>
            </Stack>
          </CardContent>
        </Card>
      )}

      <Typography variant="h6" sx={{ fontWeight: 600, mb: 1.5 }}>Histórico</Typography>
      <Card variant="outlined">
        <CardContent>
          {lead.historico.length === 0 ? (
            <Typography variant="body2" color="text.secondary">Nenhum registro no histórico.</Typography>
          ) : (
            <Stack spacing={1.5} divider={<Divider />}>
              {lead.historico.map((h) => (
                <Box key={h.id}>
                  <Typography variant="caption" color="text.secondary">{formatarDataHora(h.ocorridaEm)}</Typography>
                  <Typography variant="body2">
                    {h.statusNovo ? `Status alterado para "${LEAD_STATUS_LABEL[h.statusNovo]}"` : "Interação registrada"}
                  </Typography>
                  {h.observacao && (
                    <Typography variant="body2" color="text.secondary">{h.observacao}</Typography>
                  )}
                </Box>
              ))}
            </Stack>
          )}
        </CardContent>
      </Card>

      <ConfirmDialog
        open={descartarOpen}
        title="Descartar lead"
        description={`Descartar "${lead.nome}"? Essa ação é definitiva.`}
        confirmLabel="Descartar"
        destructive
        loading={acaoLoading}
        onConfirm={handleDescartar}
        onClose={() => setDescartarOpen(false)}
      >
        <FormControl fullWidth size="small" error={!!motivoErro} sx={{ mt: 2 }} required>
          <InputLabel id="motivo-descarte-label">Motivo</InputLabel>
          <Select
            labelId="motivo-descarte-label"
            label="Motivo"
            value={motivoDescarte}
            onChange={(e) => { setMotivoDescarte(e.target.value as MotivoDescarteLead); setMotivoErro(""); }}
          >
            {(Object.keys(MOTIVO_DESCARTE_LEAD_LABEL) as MotivoDescarteLead[]).map((m) => (
              <MenuItem key={m} value={m}>{MOTIVO_DESCARTE_LEAD_LABEL[m]}</MenuItem>
            ))}
          </Select>
          {motivoErro && <FormHelperText>{motivoErro}</FormHelperText>}
        </FormControl>
        <TextField
          label="Observação (opcional)"
          value={observacaoDescarte}
          onChange={(e) => setObservacaoDescarte(e.target.value)}
          size="small"
          fullWidth
          multiline
          rows={2}
          sx={{ mt: 2 }}
        />
      </ConfirmDialog>

      <ConfirmDialog
        open={convidarOpen}
        title={conviteEnviado ? "Reenviar convite" : "Enviar convite"}
        description={
          conviteEnviado
            ? `Reenviar o convite invalida o link anterior enviado a "${lead.nome}". Deseja continuar?`
            : `Enviar convite de cadastro para "${lead.nome}"?`
        }
        confirmLabel="Enviar"
        loading={convidando}
        onConfirm={handleConvidar}
        onClose={() => setConvidarOpen(false)}
      />
    </Box>
  );
}
