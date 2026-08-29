"use client";
import { useCallback, useState } from "react";
import { Box, Chip, FormControl, InputLabel, MenuItem, Select, Typography, Stack, Button, TextField } from "@mui/material";
import AlertBanner from "@/components/ui/AlertBanner";
import DataList from "@/components/ui/DataList";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import type { Column } from "@/components/ui/ResponsiveTable";
import { agendaApi } from "@/lib/api/agenda";
import { extractApiErrorInfo } from "@/lib/api/extractApiError";
import { usePaginatedList } from "@/hooks/usePaginatedList";
import type { SolicitacaoAgendamentoListItem, SolicitacaoAgendamentoStatus } from "@/types";
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
const CODE_CAPACIDADE_ESGOTADA = "solicitacao_agendamento.capacidade_esgotada";

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
  const [statusFiltro, setStatusFiltro] = useState<SolicitacaoAgendamentoStatus | "">("");
  const [acaoEmCursoId, setAcaoEmCursoId] = useState<string | null>(null);
  const [erroAcao, setErroAcao] = useState("");

  const [dialog, setDialog] = useState<{ tipo: DialogTipo; solicitacao: SolicitacaoAgendamentoListItem } | null>(null);
  const [motivoDialog, setMotivoDialog] = useState("");

  const fetcher = useCallback(
    (pagina: number, tamanhoPagina: number) =>
      agendaApi
        .listarSolicitacoes({ pagina: pagina + 1, tamanhoPagina, status: statusFiltro || undefined })
        .then((res) => res.data),
    [statusFiltro],
  );

  const { items: solicitacoes, total, page, pageSize, loading, error, setPage, setPageSize, setError, reload } =
    usePaginatedList<SolicitacaoAgendamentoListItem>({ fetcher, errorMessage: "Não foi possível carregar as solicitações." });

  const erroExibido = erroAcao || error;
  const fecharErro = () => { setErroAcao(""); setError(""); };

  const tratarErroAcao = (err: unknown, fallback: string) => {
    const info = extractApiErrorInfo(err);
    setErroAcao(info.code === CODE_CAPACIDADE_ESGOTADA ? ERRO_CAPACIDADE_ESGOTADA : (info.message ?? fallback));
  };

  const handleConfirmar = async (s: SolicitacaoAgendamentoListItem) => {
    setErroAcao("");
    setAcaoEmCursoId(s.id);
    try {
      await agendaApi.confirmarSolicitacao(s.id);
      reload();
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
    setErroAcao("");
    setAcaoEmCursoId(solicitacao.id);
    try {
      if (tipo === "recusar") {
        await agendaApi.recusarSolicitacao(solicitacao.id, motivoDialog.trim() || null);
      } else {
        await agendaApi.cancelarSolicitacao(solicitacao.id, motivoDialog.trim() || null);
      }
      setDialog(null);
      reload();
    } catch (err) {
      tratarErroAcao(err, "Não foi possível concluir a ação.");
    } finally {
      setAcaoEmCursoId(null);
    }
  };

  return (
    <>
      <AlertBanner open={!!erroExibido} message={erroExibido} onClose={fecharErro} />

      <Box sx={{ display: "flex", justifyContent: "flex-end", mb: 2 }}>
        <FormControl size="small" sx={{ minWidth: 160 }}>
          <InputLabel id="solicitacao-status-label">Status</InputLabel>
          <Select
            labelId="solicitacao-status-label"
            value={statusFiltro}
            label="Status"
            onChange={(e) => {
              setStatusFiltro(e.target.value as SolicitacaoAgendamentoStatus | "");
              setPage(0);
            }}
          >
            <MenuItem value="">Todos</MenuItem>
            {(Object.keys(SOLICITACAO_AGENDAMENTO_STATUS_LABEL) as SolicitacaoAgendamentoStatus[]).map((s) => (
              <MenuItem key={s} value={s}>{SOLICITACAO_AGENDAMENTO_STATUS_LABEL[s]}</MenuItem>
            ))}
          </Select>
        </FormControl>
      </Box>

      <DataList
        loading={loading}
        items={solicitacoes}
        emptyMessage="Nenhuma solicitação de agendamento ainda. Solicitações chegam pelo agente conversacional do seu perfil público."
        columns={COLUMNS}
        rowKey={(s) => s.id}
        pagination={{ count: total, page, rowsPerPage: pageSize, onPageChange: setPage, onRowsPerPageChange: setPageSize }}
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
