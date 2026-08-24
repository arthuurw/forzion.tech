"use client";
import { useEffect, useState } from "react";
import { Chip, Typography, Stack, Button, TextField } from "@mui/material";
import AlertBanner from "@/components/ui/AlertBanner";
import DataList from "@/components/ui/DataList";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import type { Column } from "@/components/ui/ResponsiveTable";
import { agendaApi } from "@/lib/api/agenda";
import { extractApiError, extractApiErrorInfo } from "@/lib/api/extractApiError";
import type { SolicitacaoAgendamentoListItem } from "@/types";
import {
  SOLICITACAO_AGENDAMENTO_STATUS_LABEL,
  SOLICITACAO_AGENDAMENTO_STATUS_COLOR,
  TIPO_CONTATO_LEAD_LABEL,
} from "@/lib/constants/labels";

const COLUMNS: Column[] = [
  { label: "Serviço" },
  { label: "Horário" },
  { label: "Lead" },
  { label: "Status" },
  { label: "Ações", align: "right" },
];

const ERRO_CAPACIDADE_ESGOTADA = "A capacidade máxima deste horário já foi atingida.";

type DialogTipo = "recusar" | "cancelar";

// Sem timeZone: "UTC" (ao contrário de formatarIntervaloPontual da página de bloqueios)
// — aqui o objetivo é o horário LOCAL do navegador do treinador (AGF4-32), não os
// dígitos crus gravados.
function formatarIntervaloSolicitacao(inicioUtc: string, fimUtc: string): string {
  const inicio = new Date(inicioUtc);
  const fim = new Date(fimUtc);
  const data = inicio.toLocaleDateString("pt-BR");
  const horaInicio = inicio.toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" });
  const horaFim = fim.toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" });
  return `${data} ${horaInicio} — ${horaFim}`;
}

export default function SolicitacoesTab() {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [solicitacoes, setSolicitacoes] = useState<SolicitacaoAgendamentoListItem[]>([]);
  const [acaoEmCursoId, setAcaoEmCursoId] = useState<string | null>(null);

  const [dialog, setDialog] = useState<{ tipo: DialogTipo; solicitacao: SolicitacaoAgendamentoListItem } | null>(null);
  const [motivoDialog, setMotivoDialog] = useState("");

  const carregar = () => {
    setLoading(true);
    agendaApi
      .listarSolicitacoes()
      .then((res) => setSolicitacoes(res.data.items))
      .catch((err) => setError(extractApiError(err, "Não foi possível carregar as solicitações.")))
      .finally(() => setLoading(false));
  };

  useEffect(() => { carregar(); }, []);

  const tratarErroAcao = (err: unknown, fallback: string) => {
    const info = extractApiErrorInfo(err);
    setError(info.status === 409 ? ERRO_CAPACIDADE_ESGOTADA : (info.message ?? fallback));
  };

  const handleConfirmar = async (s: SolicitacaoAgendamentoListItem) => {
    setError("");
    setAcaoEmCursoId(s.id);
    try {
      await agendaApi.confirmarSolicitacao(s.id);
      carregar();
    } catch (err) {
      tratarErroAcao(err, "Não foi possível confirmar a solicitação.");
    } finally {
      setAcaoEmCursoId(null);
    }
  };

  const abrirDialog = (tipo: DialogTipo, s: SolicitacaoAgendamentoListItem) => {
    setMotivoDialog("");
    setDialog({ tipo, solicitacao: s });
  };

  const handleConfirmarDialog = async () => {
    if (!dialog) return;
    const { tipo, solicitacao } = dialog;
    setError("");
    setAcaoEmCursoId(solicitacao.id);
    try {
      if (tipo === "recusar") {
        await agendaApi.recusarSolicitacao(solicitacao.id, motivoDialog.trim() || null);
      } else {
        await agendaApi.cancelarSolicitacao(solicitacao.id, motivoDialog.trim() || null);
      }
      setDialog(null);
      carregar();
    } catch (err) {
      tratarErroAcao(err, "Não foi possível concluir a ação.");
    } finally {
      setAcaoEmCursoId(null);
    }
  };

  return (
    <>
      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <DataList
        loading={loading}
        items={solicitacoes}
        emptyMessage="Nenhuma solicitação de agendamento ainda. Solicitações chegam pelo agente conversacional do seu perfil público."
        columns={COLUMNS}
        rowKey={(s) => s.id}
        renderCell={(s, i) => {
          if (i === 0) return s.pacoteNome;
          if (i === 1) return formatarIntervaloSolicitacao(s.inicioUtc, s.fimUtc);
          if (i === 2) {
            if (s.leadAnonimizado) {
              return <Typography variant="body2" color="text.secondary">Lead anonimizado</Typography>;
            }
            return (
              <>
                <Typography variant="body2">{s.leadNome}</Typography>
                <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                  {TIPO_CONTATO_LEAD_LABEL[s.leadContatoTipo]}: {s.leadContatoValor}
                </Typography>
              </>
            );
          }
          if (i === 3) {
            return (
              <Chip
                label={SOLICITACAO_AGENDAMENTO_STATUS_LABEL[s.status]}
                color={SOLICITACAO_AGENDAMENTO_STATUS_COLOR[s.status]}
                size="small"
              />
            );
          }
          const emCurso = acaoEmCursoId === s.id;
          if (s.status === "PendenteAgente") {
            return (
              <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end" }}>
                <Button size="small" variant="contained" disabled={emCurso} onClick={() => handleConfirmar(s)}>
                  Confirmar
                </Button>
                <Button size="small" variant="outlined" color="error" disabled={emCurso} onClick={() => abrirDialog("recusar", s)}>
                  Recusar
                </Button>
              </Stack>
            );
          }
          if (s.status === "Confirmada") {
            return (
              <Stack direction="row" sx={{ justifyContent: "flex-end" }}>
                <Button size="small" variant="outlined" color="error" disabled={emCurso} onClick={() => abrirDialog("cancelar", s)}>
                  Cancelar
                </Button>
              </Stack>
            );
          }
          return null;
        }}
      />

      <ConfirmDialog
        open={!!dialog}
        title={dialog?.tipo === "recusar" ? "Recusar solicitação" : "Cancelar agendamento"}
        description={
          dialog?.tipo === "recusar"
            ? "Recusar esta solicitação? O consumidor não é notificado automaticamente pelo sistema."
            : "Cancelar este agendamento confirmado? A vaga volta a ficar disponível."
        }
        confirmLabel={dialog?.tipo === "recusar" ? "Recusar" : "Cancelar agendamento"}
        destructive
        loading={!!dialog && acaoEmCursoId === dialog.solicitacao.id}
        onConfirm={handleConfirmarDialog}
        onClose={() => setDialog(null)}
      >
        <TextField
          label="Motivo (opcional)"
          value={motivoDialog}
          onChange={(e) => setMotivoDialog(e.target.value)}
          size="small"
          fullWidth
          multiline
          rows={2}
          sx={{ mt: 2 }}
        />
      </ConfirmDialog>
    </>
  );
}
