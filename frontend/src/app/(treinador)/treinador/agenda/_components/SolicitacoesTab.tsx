"use client";
import { useEffect, useState } from "react";
import { Chip, Typography } from "@mui/material";
import AlertBanner from "@/components/ui/AlertBanner";
import DataList from "@/components/ui/DataList";
import type { Column } from "@/components/ui/ResponsiveTable";
import { agendaApi } from "@/lib/api/agenda";
import { extractApiError } from "@/lib/api/extractApiError";
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
];

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

  const carregar = () => {
    setLoading(true);
    agendaApi
      .listarSolicitacoes()
      .then((res) => setSolicitacoes(res.data.items))
      .catch((err) => setError(extractApiError(err, "Não foi possível carregar as solicitações.")))
      .finally(() => setLoading(false));
  };

  useEffect(() => { carregar(); }, []);

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
          return (
            <Chip
              label={SOLICITACAO_AGENDAMENTO_STATUS_LABEL[s.status]}
              color={SOLICITACAO_AGENDAMENTO_STATUS_COLOR[s.status]}
              size="small"
            />
          );
        }}
      />
    </>
  );
}
