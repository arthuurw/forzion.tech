"use client";
import { useEffect, useState } from "react";
import { Box, Typography, Button, Chip, Stack, Paper, Divider } from "@mui/material";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import { pagamentoApi } from "@/lib/api/pagamento";
import { extractApiError } from "@/lib/api/extractApiError";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import PageHeader from "@/components/ui/PageHeader";
import type { OnboardingStatusResponse, PagamentoStatus, RecebimentoTreinadorResponse } from "@/types";

const COOLDOWN_ACEITE =
  "Confirma a alteração? Um novo ajuste só poderá ser feito depois de 90 dias (3 meses).";

const STATUS_COR: Record<PagamentoStatus, "success" | "warning" | "error" | "default"> = {
  Pago: "success",
  Pendente: "default",
  Expirado: "default",
  Falhou: "default",
  Estornado: "warning",
  EmDisputa: "error",
};

const STATUS_ROTULO: Record<PagamentoStatus, string> = {
  Pago: "Pago",
  Pendente: "Pendente",
  Expirado: "Expirado",
  Falhou: "Falhou",
  Estornado: "Estornado",
  EmDisputa: "Em disputa",
};

const formatBRL = (v: number) => v.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });

function HistoricoRecebimentos() {
  const [itens, setItens] = useState<RecebimentoTreinadorResponse[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [taxaPlataforma, setTaxaPlataforma] = useState<number | null>(null);
  const [carregado, setCarregado] = useState(false);
  const [carregando, setCarregando] = useState(false);
  const [erro, setErro] = useState("");

  const carregarPagina = async (proximo?: string) => {
    setCarregando(true);
    setErro("");
    try {
      const { data } = await pagamentoApi.listarRecebimentos(proximo);
      setItens((prev) => (proximo ? [...prev, ...data.itens] : data.itens));
      setCursor(data.proximoCursor);
      setTaxaPlataforma(data.taxaPlataformaPercent);
    } catch {
      setErro("Não foi possível carregar os recebimentos.");
    } finally {
      setCarregando(false);
      setCarregado(true);
    }
  };

  useEffect(() => { carregarPagina(); }, []);

  return (
    <Box sx={{ mt: 4 }}>
      <Stack direction="row" spacing={1} sx={{ alignItems: "center", mb: 0.5, flexWrap: "wrap" }}>
        <Typography variant="h6">Histórico de recebimentos</Typography>
        {taxaPlataforma != null && (
          <Chip
            label={`Taxa da plataforma: ${taxaPlataforma}%`}
            color="primary"
            variant="outlined"
            size="small"
          />
        )}
      </Stack>
      <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 2 }}>
        Líquido estimado já descontada a taxa da plataforma{taxaPlataforma != null ? ` de ${taxaPlataforma}%` : ""}.
      </Typography>

      <AlertBanner open={!!erro} severity="error" message={erro} />

      {!carregado && carregando ? (
        <LoadingSpinner />
      ) : itens.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          Nenhum recebimento ainda. Cobranças pagas pelos seus alunos aparecem aqui.
        </Typography>
      ) : (
        <Stack spacing={1} component="ul" sx={{ listStyle: "none", p: 0, m: 0 }}>
          {itens.map((r) => (
            <Paper key={r.pagamentoId} variant="outlined" component="li" sx={{ p: 2 }}>
              <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "flex-start" }} spacing={1}>
                <Box>
                  <Typography variant="subtitle2">{r.nomeAluno}</Typography>
                  <Typography variant="caption" color="text.secondary">
                    {new Date(r.dataPagamento ?? r.createdAt).toLocaleDateString("pt-BR")} · {r.metodo}
                  </Typography>
                </Box>
                <Chip label={STATUS_ROTULO[r.status]} color={STATUS_COR[r.status]} size="small" />
              </Stack>
              <Stack direction="row" spacing={2} sx={{ mt: 1, flexWrap: "wrap" }}>
                <Typography variant="body2" color="text.secondary">Bruto: {formatBRL(r.bruto)}</Typography>
                <Typography variant="body2" color="text.secondary">
                  Taxa: {r.taxaPercent != null ? `${r.taxaPercent}%` : "—"}
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: "medium" }}>
                  Líquido estimado: {r.liquidoEstimado != null ? formatBRL(r.liquidoEstimado) : "—"}
                </Typography>
              </Stack>
            </Paper>
          ))}
        </Stack>
      )}

      {cursor && (
        <Button onClick={() => carregarPagina(cursor)} disabled={carregando} sx={{ mt: 2 }}>
          {carregando ? "Carregando..." : "Carregar mais"}
        </Button>
      )}
    </Box>
  );
}

export default function PagamentosTreinadorPage() {
  const [status, setStatus] = useState<OnboardingStatusResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [iniciando, setIniciando] = useState(false);
  const [error, setError] = useState("");
  const [confirmarTroca, setConfirmarTroca] = useState(false);
  const [trocando, setTrocando] = useState(false);
  const [sucesso, setSucesso] = useState("");
  const [previewTexto, setPreviewTexto] = useState("");

  const carregar = async () => {
    try {
      const res = await pagamentoApi.verificarOnboarding();
      setStatus(res.data);
    } catch (err) {
      setError(extractApiError(err, "Erro ao verificar status do cadastro Stripe."));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { carregar(); }, []);

  const iniciarOnboarding = async () => {
    setIniciando(true);
    setError("");
    try {
      const origin = window.location.origin;
      const res = await pagamentoApi.iniciarOnboarding(
        `${origin}/treinador/onboarding/retorno`,
        `${origin}/treinador/pagamentos`,
      );
      window.location.href = res.data.url;
    } catch (err) {
      setError(extractApiError(err, "Erro ao iniciar cadastro. Tente novamente."));
      setIniciando(false);
    }
  };

  if (loading) return <LoadingSpinner fullPage />;

  const externo = status?.modoPagamentoAluno === "Externo";
  const liberadoEm = status?.modoPagamentoPodeAlterarEm ? new Date(status.modoPagamentoPodeAlterarEm) : null;
  const cooldownAtivo = !!liberadoEm && liberadoEm > new Date();

  const abrirTroca = async () => {
    setError("");
    setSucesso("");
    setPreviewTexto("");
    setConfirmarTroca(true);
    try {
      const { data } = await pagamentoApi.previewModoPagamento();
      setPreviewTexto(
        externo
          ? `Até ${data.vinculosCobravelSemAssinatura} assinatura(s) serão criadas para seus alunos ativos com pacote cobrável.`
          : `${data.assinaturasAtivasAlunos} assinatura(s) ativa(s) de alunos serão canceladas.`,
      );
    } catch {
      setPreviewTexto("Não foi possível pré-calcular o impacto. A troca continua segura.");
    }
  };

  const alternarModo = async () => {
    setTrocando(true);
    setError("");
    try {
      const res = await pagamentoApi.alterarModoPagamento(externo ? "Plataforma" : "Externo");
      const { assinaturasCriadas, vinculosIgnorados } = res.data;
      setSucesso(
        externo
          ? `${assinaturasCriadas} assinatura(s) criada(s) para seus alunos ativos.` +
            (vinculosIgnorados > 0
              ? ` ${vinculosIgnorados} vínculo(s) sem pacote cobrável foram ignorados.`
              : "")
          : "Cobranças via plataforma encerradas. Você passou a cobrar por fora.",
      );
      setConfirmarTroca(false);
      await carregar();
    } catch (err) {
      setError(extractApiError(err, "Não foi possível alterar o modo de pagamento. Tente novamente."));
    } finally {
      setTrocando(false);
    }
  };

  const trocaDialog = (
    <ConfirmDialog
      open={confirmarTroca}
      title={externo ? "Voltar a receber pela plataforma" : "Receber por fora da plataforma"}
      description={
        (externo
          ? `Será necessário ter a conta Stripe configurada. Assinaturas serão criadas para seus alunos ativos e a cobrança via plataforma recomeça. ${COOLDOWN_ACEITE}`
          : `Suas assinaturas ativas de alunos serão canceladas e você passará a cobrar manualmente, por fora da plataforma. ${COOLDOWN_ACEITE}`) +
        (previewTexto ? ` ${previewTexto}` : "")
      }
      destructive={!externo}
      confirmLabel={externo ? "Voltar à plataforma" : "Receber por fora"}
      cancelLabel="Voltar"
      loading={trocando}
      onConfirm={alternarModo}
      onClose={() => { if (!trocando) { setConfirmarTroca(false); setError(""); } }}
    >
      <AlertBanner open={!!error} severity="error" message={error} />
    </ConfirmDialog>
  );

  const trocaAcao = (label: string) => (
    <>
      <Divider />
      <Button
        variant="outlined"
        color={externo ? "primary" : "error"}
        onClick={abrirTroca}
        disabled={cooldownAtivo}
        sx={{ alignSelf: "flex-start" }}
      >
        {label}
      </Button>
      {cooldownAtivo && liberadoEm && (
        <Typography variant="caption" color="text.secondary">
          Novo ajuste disponível em {liberadoEm.toLocaleDateString("pt-BR")}
        </Typography>
      )}
    </>
  );

  if (externo) {
    return (
      <Box sx={{ p: { xs: 2, md: 4 }, maxWidth: 600 }}>
        <PageHeader title="Recebimentos" subtitle="Você recebe seus alunos por fora da plataforma." />

        {!confirmarTroca && <AlertBanner open={!!error} severity="error" message={error} />}
        <AlertBanner open={!!sucesso} severity="success" message={sucesso} onClose={() => setSucesso("")} />

        <Paper variant="outlined" sx={{ p: { xs: 2, md: 3 } }}>
          <Stack spacing={1.5}>
            <Chip label="Pagamento externo" color="default" size="small" sx={{ alignSelf: "flex-start" }} />
            <Typography variant="body2" color="text.secondary">
              Não há cobrança automática nem cadastro Stripe neste modo. Combine o valor direto com
              cada aluno e gerencie o acesso manualmente: ao desvincular um aluno, ele mantém apenas
              o histórico (somente leitura) até um novo vínculo.
            </Typography>
            {trocaAcao("Voltar a receber pela plataforma")}
          </Stack>
        </Paper>

        <Box sx={{ mt: 4 }}>
          <Typography variant="h6" sx={{ mb: 0.5 }}>Histórico de recebimentos</Typography>
          <Typography variant="body2" color="text.secondary">
            No modo externo, os pagamentos são combinados direto com o aluno e não passam pela plataforma — não há
            histórico de recebimentos aqui.
          </Typography>
        </Box>

        {trocaDialog}
      </Box>
    );
  }

  return (
    <Box sx={{ p: { xs: 2, md: 4 }, maxWidth: 600 }}>
      <PageHeader title="Recebimentos" subtitle="Configure sua conta Stripe para receber pagamentos dos alunos via Pix." />

      {!confirmarTroca && <AlertBanner open={!!error} severity="error" message={error} />}
      <AlertBanner open={!!sucesso} severity="success" message={sucesso} onClose={() => setSucesso("")} />

      <Paper variant="outlined" sx={{ p: { xs: 2, md: 3 } }}>
        <Stack spacing={2}>
          <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
            <Typography variant="subtitle1" sx={{ fontWeight: "medium" }}>Status da conta</Typography>
            {status?.onboardingCompleto ? (
              <Chip icon={<CheckCircleIcon />} label="Ativo" color="success" size="small" />
            ) : status?.contaConfigurada ? (
              <Chip label="Cadastro incompleto" color="warning" size="small" />
            ) : (
              <Chip label="Não configurado" color="default" size="small" />
            )}
          </Stack>

          {status?.onboardingCompleto ? (
            <Typography variant="body2" color="text.secondary">
              Sua conta está ativa. Você pode receber pagamentos dos alunos via Pix.
            </Typography>
          ) : (
            <>
              <Typography variant="body2" color="text.secondary">
                {status?.contaConfigurada
                  ? "Seu cadastro na Stripe está incompleto. Clique abaixo para continuar."
                  : "Você ainda não configurou sua conta de recebimentos. O processo leva menos de 5 minutos."}
              </Typography>
              <Button
                variant="contained"
                endIcon={<OpenInNewIcon />}
                onClick={iniciarOnboarding}
                disabled={iniciando}
                sx={{ alignSelf: "flex-start" }}
              >
                {iniciando ? "Redirecionando..." : status?.contaConfigurada ? "Continuar cadastro" : "Configurar recebimentos"}
              </Button>
            </>
          )}

          {trocaAcao("Receber por fora da plataforma")}
        </Stack>
      </Paper>

      <HistoricoRecebimentos />

      {trocaDialog}
    </Box>
  );
}
