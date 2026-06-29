"use client";
import { useCallback, useEffect, useRef, useState } from "react";
import {
  // eslint-disable-next-line no-restricted-imports -- painéis de status inline contextuais (downgrade agendado/inadimplente/aguardando pix); AlertBanner é o canal de feedback dismissível
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Dialog,
  DialogContent,
  DialogTitle,
  Divider,
  Stack,
  Typography,
} from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import SwapHorizIcon from "@mui/icons-material/SwapHoriz";
import dayjs from "dayjs";
import { pagamentoApi } from "@/lib/api/pagamento";
import { useAuth } from "@/lib/auth/context";
import { extractApiError, extractApiErrorInfo } from "@/lib/api/extractApiError";
import { baixarMeusDados as baixarMeusDadosBlob } from "@/lib/utils/downloadBlob";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import PageHeader from "@/components/ui/PageHeader";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import { formatarBRL } from "@/lib/utils/formatting";
import type {
  AssinaturaTreinadorResponse,
  ContratarPlanoTreinadorResponse,
  PlanoPlataformaResponse,
  TrocarPlanoTreinadorResponse,
} from "@/types";

type Etapa = "idle" | "confirmando" | "pagando" | "sucesso";
type ModoFluxo = "troca" | "contratar";

export const CANCELAR_PLANO_DESCRICAO =
  "Seu acesso será encerrado imediatamente. Ação irreversível pelo portal.";

const OFFBOARDING_CODE = "assinatura_treinador.offboarding_necessario";

const OFFBOARDING_MENSAGEM =
  "Encerre os vínculos com seus alunos antes de cancelar o plano.";

export default function PlanoTreinadorPage() {
  const [assinatura, setAssinatura] = useState<AssinaturaTreinadorResponse | null>(null);
  const [planos, setPlanos] = useState<PlanoPlataformaResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [erro, setErro] = useState("");
  const [etapa, setEtapa] = useState<Etapa>("idle");
  const [modoFluxo, setModoFluxo] = useState<ModoFluxo | null>(null);
  const [planoSelecionado, setPlanoSelecionado] = useState<PlanoPlataformaResponse | null>(null);
  const [trocaResp, setTrocaResp] = useState<TrocarPlanoTreinadorResponse | null>(null);
  const [contratoResp, setContratoResp] = useState<ContratarPlanoTreinadorResponse | null>(null);
  const [processando, setProcessando] = useState(false);
  const pollingRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const delayRef = useRef(5_000);
  const deadlineRef = useRef(0);
  const mountedRef = useRef(true);
  const { logout } = useAuth();
  const [confirmarCancelar, setConfirmarCancelar] = useState(false);
  const [cancelando, setCancelando] = useState(false);
  const [erroCancelar, setErroCancelar] = useState("");
  const [baixando, setBaixando] = useState(false);

  const carregar = useCallback(async () => {
    setErro("");
    const [aRes, pRes] = await Promise.allSettled([
      pagamentoApi.obterAssinaturaTreinador(),
      pagamentoApi.listarPlanosPlataforma(),
    ]);
    if (aRes.status === "fulfilled") {
      setAssinatura(aRes.value.data);
    } else {
      setErro("Erro ao carregar informações do plano.");
    }
    if (pRes.status === "fulfilled") {
      setPlanos(pRes.value.data);
    } else {
      setErro("Erro ao carregar informações do plano.");
    }
    setLoading(false);
  }, []);

  useEffect(() => {
    carregar();
  }, [carregar]);

  const pararPolling = useCallback(() => {
    if (pollingRef.current) {
      clearTimeout(pollingRef.current);
      pollingRef.current = null;
    }
  }, []);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
      pararPolling();
    };
  }, [pararPolling]);

  const iniciarTroca = (plano: PlanoPlataformaResponse) => {
    setPlanoSelecionado(plano);
    setModoFluxo("troca");
    setEtapa("confirmando");
    setErro("");
  };

  const iniciarContratar = (plano: PlanoPlataformaResponse) => {
    setPlanoSelecionado(plano);
    setModoFluxo("contratar");
    setEtapa("confirmando");
    setErro("");
  };

  const confirmarTroca = async () => {
    if (!planoSelecionado) return;
    setProcessando(true);
    setErro("");
    try {
      const res = await pagamentoApi.trocarPlano(planoSelecionado.planoId);
      setTrocaResp(res.data);

      if (res.data.tipo === "Downgrade" || res.data.tipo === "UpgradeImediato") {
        setEtapa("sucesso");
        await carregar();
      } else {
        setEtapa("pagando");
        if (res.data.pagamentoId && res.data.metodoPagamento === "Pix") {
          iniciarPolling(res.data.pagamentoId);
        }
      }
    } catch (err) {
      setErro(extractApiError(err, "Erro ao processar troca de plano. Tente novamente."));
    } finally {
      setProcessando(false);
    }
  };

  const confirmarContratar = async () => {
    if (!planoSelecionado) return;
    setProcessando(true);
    setErro("");
    try {
      const res = await pagamentoApi.contratarPlano(planoSelecionado.planoId);
      setContratoResp(res.data);
      setEtapa("pagando");
      if (res.data.pagamentoId && res.data.metodoPagamento === "Pix") {
        iniciarPolling(res.data.pagamentoId);
      }
    } catch (err) {
      setErro(extractApiError(err, "Erro ao processar contratação de plano. Tente novamente."));
    } finally {
      setProcessando(false);
    }
  };

  const iniciarPolling = (pagamentoId: string) => {
    pararPolling();
    delayRef.current = 5_000;
    deadlineRef.current = Date.now() + 180_000;

    const tick = async () => {
      if (!mountedRef.current) return;
      if (document.hidden) {
        pollingRef.current = setTimeout(tick, delayRef.current);
        return;
      }
      if (Date.now() >= deadlineRef.current) {
        setErro("Verificação expirou. Recarregue a página.");
        return;
      }
      try {
        const res = await pagamentoApi.obterStatusPagamentoTreinador(pagamentoId);
        if (!mountedRef.current) return;
        if (res.data.status === "Pago") {
          pararPolling();
          setEtapa("sucesso");
          await carregar();
          return;
        }
      } catch {
      }
      if (!mountedRef.current) return;
      delayRef.current = Math.min(delayRef.current * 2, 30_000);
      pollingRef.current = setTimeout(tick, delayRef.current);
    };

    pollingRef.current = setTimeout(tick, delayRef.current);
  };

  const fecharDialog = () => {
    pararPolling();
    setEtapa("idle");
    setModoFluxo(null);
    setPlanoSelecionado(null);
    setTrocaResp(null);
    setContratoResp(null);
    setErro("");
  };

  const baixarMeusDados = async () => {
    setBaixando(true);
    setErroCancelar("");
    try {
      await baixarMeusDadosBlob();
    } catch (err) {
      setErroCancelar(extractApiError(err, "Erro ao exportar dados."));
    } finally {
      setBaixando(false);
    }
  };

  const cancelarPlano = async () => {
    setCancelando(true);
    setErroCancelar("");
    try {
      await pagamentoApi.cancelarPlanoTreinador();
      await logout();
    } catch (err) {
      const { code, status } = extractApiErrorInfo(err);
      if (code === OFFBOARDING_CODE) {
        setErroCancelar(OFFBOARDING_MENSAGEM);
      } else if (status === 404) {
        setErroCancelar("Nenhuma assinatura ativa para cancelar.");
      } else {
        setErroCancelar(extractApiError(err, "Não foi possível cancelar o plano. Tente novamente."));
      }
    } finally {
      setCancelando(false);
    }
  };

  const fecharCancelar = () => {
    if (cancelando) return;
    setConfirmarCancelar(false);
    setErroCancelar("");
  };

  const planoAtual = assinatura
    ? planos.find((p) => p.planoId === assinatura.planoPlataformaId) ?? null
    : null;

  const precoAtual = assinatura?.valor ?? planoAtual?.preco ?? null;

  const inadimplente = assinatura?.status === "Inadimplente";

  const opcoesTroca = assinatura
    ? planos.filter(
        (p) =>
          p.tier !== "Elite" &&
          p.isAtivo &&
          (assinatura.status === "Inadimplente" || p.planoId !== assinatura.planoPlataformaId)
      )
    : [];

  const opcoesContratar = !assinatura
    ? planos.filter((p) => p.tier !== "Elite" && p.isAtivo)
    : [];

  const estimarProracao = (novoPlano: PlanoPlataformaResponse) => {
    if (!assinatura || precoAtual == null) return null;
    const diasRestantes = Math.max(
      0,
      dayjs(assinatura.dataProximaCobranca).diff(dayjs(), "day")
    );
    const proracao = ((novoPlano.preco - precoAtual) * diasRestantes) / 30;
    return proracao > 0 ? Math.round(proracao * 100) / 100 : null;
  };

  const statusColor = (s: AssinaturaTreinadorResponse["status"]) => {
    if (s === "Ativa") return "success";
    if (s === "Inadimplente") return "error";
    if (s === "Cancelada") return "default";
    return "warning";
  };

  const respPagamento = modoFluxo === "contratar" ? contratoResp : trocaResp;

  if (loading) return <LoadingSpinner />;

  return (
    <Box sx={{ p: { xs: 2.5, md: 3.5 }, maxWidth: 720 }}>
      <PageHeader
        title="Meu plano"
        subtitle="Gerencie sua assinatura e troque de plano quando precisar."
      />

      <AlertBanner open={!!erro} message={erro} />

      {assinatura && (
        <Card variant="outlined" sx={{ mb: 3 }}>
          <CardContent>
            <Stack direction="row" sx={{ alignItems: "center", justifyContent: "space-between", mb: 1, flexWrap: "wrap", gap: 1 }}>
              <Typography variant="subtitle1">
                {planoAtual?.nome ?? "Plano atual"}
              </Typography>
              <Chip
                label={assinatura.status}
                color={statusColor(assinatura.status)}
                size="small"
              />
            </Stack>
            <Typography variant="body2" color="text.secondary">
              {formatarBRL(assinatura.valor)}/mês
            </Typography>
            {assinatura.status === "Ativa" && (
              <Typography variant="body2" color="text.secondary">
                Próxima cobrança: {dayjs(assinatura.dataProximaCobranca).format("DD/MM/YYYY")}
              </Typography>
            )}
            {assinatura.planoPlataformaIdAgendado && (
              <Alert severity="info" sx={{ mt: 1.5 }} icon={false}>
                Downgrade agendado para a próxima renovação.
              </Alert>
            )}
            {assinatura.status === "Inadimplente" && (
              <Alert severity="error" sx={{ mt: 1.5 }}>
                Assinatura inadimplente. Regularize escolhendo um plano abaixo.
              </Alert>
            )}
            {(assinatura.status === "Ativa" || assinatura.status === "Inadimplente") && (
              <>
                <Divider sx={{ my: 1.5 }} />
                <Button
                  variant="outlined"
                  color="error"
                  size="small"
                  onClick={() => setConfirmarCancelar(true)}
                >
                  Cancelar plano
                </Button>
              </>
            )}
          </CardContent>
        </Card>
      )}

      {!assinatura && (
        <>
          <Typography variant="h6" sx={{ mb: 2 }}>
            Contratar plano
          </Typography>
          <Stack spacing={2}>
            {opcoesContratar.map((plano) => (
              <Card key={plano.planoId} variant="outlined">
                <CardContent>
                  <Stack direction="row" sx={{ alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 1 }}>
                    <Box sx={{ minWidth: 0 }}>
                      <Typography variant="subtitle1">{plano.nome}</Typography>
                      <Typography variant="body2" color="text.secondary">
                        {formatarBRL(plano.preco)}/mês · até {plano.maxAlunos} alunos
                      </Typography>
                    </Box>
                    <Button
                      variant="outlined"
                      size="small"
                      onClick={() => iniciarContratar(plano)}
                    >
                      Contratar
                    </Button>
                  </Stack>
                </CardContent>
              </Card>
            ))}
          </Stack>
        </>
      )}

      {assinatura && (
        <>
          <Typography variant="h6" sx={{ mb: 2 }}>
            Trocar plano
          </Typography>
          <Stack spacing={2}>
            {opcoesTroca.map((plano) => {
              const proracao = estimarProracao(plano);
              const eUpgrade = precoAtual != null && plano.preco > precoAtual;
              return (
                <Card key={plano.planoId} variant="outlined">
                  <CardContent>
                    <Stack direction="row" sx={{ alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 1 }}>
                      <Box sx={{ minWidth: 0 }}>
                        <Typography variant="subtitle1">
                          {plano.nome}
                        </Typography>
                        <Typography variant="body2" color="text.secondary">
                          {formatarBRL(plano.preco)}/mês · até {plano.maxAlunos} alunos
                        </Typography>
                        {inadimplente ? (
                          <Typography variant="caption" color="text.secondary">
                            Pagamento imediato para regularizar
                          </Typography>
                        ) : eUpgrade && proracao !== null && proracao > 0 ? (
                          <Typography variant="caption" color="text.secondary">
                            Proração estimada: {formatarBRL(proracao)}
                          </Typography>
                        ) : !eUpgrade && precoAtual != null ? (
                          <Typography variant="caption" color="text.secondary">
                            Downgrade agendado para a próxima renovação
                          </Typography>
                        ) : null}
                      </Box>
                      <Button
                        variant="outlined"
                        size="small"
                        startIcon={<SwapHorizIcon />}
                        onClick={() => iniciarTroca(plano)}
                      >
                        Trocar
                      </Button>
                    </Stack>
                  </CardContent>
                </Card>
              );
            })}
          </Stack>
        </>
      )}

      <Dialog
        open={etapa !== "idle"}
        onClose={fecharDialog}
        maxWidth="xs"
        fullWidth
        aria-describedby="plano-dialog-desc"
        slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}
      >
        <DialogTitle>
          {etapa === "confirmando" && (modoFluxo === "contratar" ? "Confirmar contratação" : "Confirmar troca de plano")}
          {etapa === "pagando" && "Pagamento"}
          {etapa === "sucesso" && (modoFluxo === "contratar" ? "Plano contratado!" : "Troca concluída")}
        </DialogTitle>
        <DialogContent id="plano-dialog-desc">
          {etapa === "confirmando" && planoSelecionado && (
            <Stack spacing={2} sx={{ mt: 1 }}>
              <AlertBanner open={!!erro} message={erro} />
              <Typography variant="body2">
                {modoFluxo === "contratar"
                  ? `Contratar o plano ${planoSelecionado.nome}.`
                  : inadimplente
                    ? `Regularizar assinatura no plano ${planoSelecionado.nome}. O pagamento será processado agora para reativar seu acesso.`
                    : precoAtual == null
                      ? `Confirmar troca para ${planoSelecionado.nome}.`
                      : planoSelecionado.preco > precoAtual
                        ? `Upgrade para ${planoSelecionado.nome}. O valor exato de proração será confirmado após processar.`
                        : `Downgrade para ${planoSelecionado.nome}. O plano muda na próxima renovação.`}
              </Typography>
              <Divider />
              <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end" }}>
                <Button onClick={fecharDialog} disabled={processando}>Cancelar</Button>
                <Button
                  variant="contained"
                  onClick={modoFluxo === "contratar" ? confirmarContratar : confirmarTroca}
                  disabled={processando}
                >
                  {processando ? <CircularProgress size={20} /> : "Confirmar"}
                </Button>
              </Stack>
            </Stack>
          )}

          {etapa === "pagando" && respPagamento && (
            <Stack spacing={2} sx={{ mt: 1 }}>
              {respPagamento.valorPagamento != null && (
                <Typography variant="body2" color="primary.main" sx={{ fontWeight: "bold" }}>
                  Valor: {formatarBRL(respPagamento.valorPagamento)}
                </Typography>
              )}
              {respPagamento.pixQrCode && (
                <>
                  <Typography variant="body2">
                    Escaneie o QR code abaixo para confirmar o pagamento.
                    {modoFluxo === "troca"
                      ? " A troca será aplicada automaticamente após a confirmação."
                      : " O plano será ativado automaticamente após a confirmação."}
                  </Typography>
                  <Box sx={{ textAlign: "center", my: 1 }}>
                    {respPagamento.pixQrCodeUrl && (
                      <Box
                        component="img"
                        src={respPagamento.pixQrCodeUrl}
                        alt="QR Code Pix"
                        sx={{ maxWidth: 200 }}
                      />
                    )}
                    <Typography variant="caption" sx={{ display: "block", wordBreak: "break-all", mt: 1 }}>
                      {respPagamento.pixQrCode}
                    </Typography>
                  </Box>
                  <Alert severity="info" icon={false}>
                    Aguardando confirmação do pagamento…
                  </Alert>
                </>
              )}
              <Button onClick={fecharDialog} variant="outlined" fullWidth>
                Fechar (verificarei depois)
              </Button>
            </Stack>
          )}

          {etapa === "sucesso" && (
            <Stack spacing={2} sx={{ mt: 1, alignItems: "center", textAlign: "center" }}>
              <CheckCircleIcon sx={{ fontSize: 56, color: "success.main" }} />
              {modoFluxo === "contratar" ? (
                <>
                  <Typography variant="body1" sx={{ fontWeight: "bold" }}>Plano contratado!</Typography>
                  <Typography variant="body2" color="text.secondary">
                    Você agora está no plano {planoSelecionado?.nome}.
                  </Typography>
                </>
              ) : trocaResp?.tipo === "Downgrade" ? (
                <>
                  <Typography variant="body1" sx={{ fontWeight: "bold" }}>Downgrade agendado</Typography>
                  <Typography variant="body2" color="text.secondary">
                    O plano {planoSelecionado?.nome} será aplicado na próxima renovação
                    {trocaResp.dataEfetivacao ? ` em ${dayjs(trocaResp.dataEfetivacao).format("DD/MM/YYYY")}` : ""}.
                  </Typography>
                </>
              ) : (
                <>
                  <Typography variant="body1" sx={{ fontWeight: "bold" }}>Plano atualizado!</Typography>
                  <Typography variant="body2" color="text.secondary">
                    Você agora está no plano {planoSelecionado?.nome}.
                  </Typography>
                </>
              )}
              <Button variant="contained" onClick={fecharDialog} fullWidth>
                Fechar
              </Button>
            </Stack>
          )}
        </DialogContent>
      </Dialog>

      <ConfirmDialog
        open={confirmarCancelar}
        title="Cancelar plano"
        description={CANCELAR_PLANO_DESCRICAO}
        destructive
        confirmLabel="Confirmar cancelamento"
        cancelLabel="Voltar"
        loading={cancelando}
        onConfirm={cancelarPlano}
        onClose={fecharCancelar}
      >
        <Stack spacing={1.5} sx={{ mt: 2 }}>
          <AlertBanner open={!!erroCancelar} message={erroCancelar} />
          <Typography variant="body2" color="text.secondary">
            Antes de cancelar, você pode baixar uma cópia dos seus dados.
          </Typography>
          <Button
            variant="text"
            onClick={baixarMeusDados}
            disabled={baixando || cancelando}
            sx={{ alignSelf: "flex-start" }}
          >
            {baixando ? "Baixando..." : "Baixar meus dados"}
          </Button>
        </Stack>
      </ConfirmDialog>
    </Box>
  );
}
