"use client";
import { useCallback, useEffect, useRef, useState } from "react";
import {
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
import { formatarBRL } from "@/lib/utils/formatting";
import type {
  AssinaturaTreinadorResponse,
  PlanoPlataformaResponse,
  TrocarPlanoTreinadorResponse,
} from "@/types";

type Etapa = "idle" | "confirmando" | "pagando" | "sucesso";

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
  const [planoSelecionado, setPlanoSelecionado] = useState<PlanoPlataformaResponse | null>(null);
  const [trocaResp, setTrocaResp] = useState<TrocarPlanoTreinadorResponse | null>(null);
  const [processando, setProcessando] = useState(false);
  const pollingRef = useRef<ReturnType<typeof setInterval> | null>(null);
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
    }
    if (pRes.status === "fulfilled") {
      setPlanos(pRes.value.data.filter((p) => p.tier !== "Elite" && p.isAtivo));
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
      clearInterval(pollingRef.current);
      pollingRef.current = null;
    }
  }, []);

  useEffect(() => () => pararPolling(), [pararPolling]);

  const iniciarTroca = async (plano: PlanoPlataformaResponse) => {
    setPlanoSelecionado(plano);
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
    } catch {
      setErro("Erro ao processar troca de plano. Tente novamente.");
    } finally {
      setProcessando(false);
    }
  };

  const iniciarPolling = (pagamentoId: string) => {
    pararPolling();
    pollingRef.current = setInterval(async () => {
      try {
        const res = await pagamentoApi.obterStatusPagamentoTreinador(pagamentoId);
        if (res.data.status === "Pago") {
          pararPolling();
          setEtapa("sucesso");
          await carregar();
        }
      } catch {
        // continua polling
      }
    }, 5000);
  };

  const fecharDialog = () => {
    pararPolling();
    setEtapa("idle");
    setPlanoSelecionado(null);
    setTrocaResp(null);
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
    ? planos.find((p) => p.planoId === assinatura.planoPlataformaId)
    : null;

  const estimarProracao = (novoPlano: PlanoPlataformaResponse) => {
    if (!assinatura || !planoAtual) return null;
    const diasRestantes = Math.max(
      0,
      dayjs(assinatura.dataProximaCobranca).diff(dayjs(), "day")
    );
    const proracao = ((novoPlano.preco - planoAtual.preco) * diasRestantes) / 30;
    return proracao > 0 ? Math.round(proracao * 100) / 100 : null;
  };

  const statusColor = (s: AssinaturaTreinadorResponse["status"]) => {
    if (s === "Ativa") return "success";
    if (s === "Inadimplente") return "error";
    if (s === "Cancelada") return "default";
    return "warning";
  };

  if (loading) {
    return (
      <Box sx={{ p: 4, display: "flex", justifyContent: "center" }}>
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box sx={{ p: { xs: 2.5, md: 3.5 }, maxWidth: 720 }}>
      <Typography variant="h5" sx={{ fontWeight: "bold", mb: 0.5 }}>
        Meu plano
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        Gerencie sua assinatura e troque de plano quando precisar.
      </Typography>

      {erro && <Alert severity="error" sx={{ mb: 2 }}>{erro}</Alert>}

      {assinatura && (
        <Card variant="outlined" sx={{ mb: 3 }}>
          <CardContent>
            <Stack direction="row" sx={{ alignItems: "center", justifyContent: "space-between", mb: 1, flexWrap: "wrap", gap: 1 }}>
              <Typography variant="subtitle1" sx={{ fontWeight: "bold" }}>
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

      <Typography variant="h6" sx={{ fontWeight: "bold", mb: 2 }}>
        Trocar plano
      </Typography>

      <Stack spacing={2}>
        {planos
          .filter((p) => p.planoId !== assinatura?.planoPlataformaId)
          .map((plano) => {
            const proracao = estimarProracao(plano);
            const eUpgrade = planoAtual && plano.preco > planoAtual.preco;
            return (
              <Card key={plano.planoId} variant="outlined">
                <CardContent>
                  <Stack direction="row" sx={{ alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 1 }}>
                    <Box sx={{ minWidth: 0 }}>
                      <Typography variant="subtitle1" sx={{ fontWeight: "bold" }}>
                        {plano.nome}
                      </Typography>
                      <Typography variant="body2" color="text.secondary">
                        {formatarBRL(plano.preco)}/mês · até {plano.maxAlunos} alunos
                      </Typography>
                      {eUpgrade && proracao !== null && proracao > 0 && (
                        <Typography variant="caption" color="text.secondary">
                          Proração estimada: {formatarBRL(proracao)}
                        </Typography>
                      )}
                      {!eUpgrade && planoAtual && (
                        <Typography variant="caption" color="text.secondary">
                          Downgrade agendado para a próxima renovação
                        </Typography>
                      )}
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

      <Dialog
        open={etapa !== "idle"}
        onClose={fecharDialog}
        maxWidth="xs"
        fullWidth
        aria-describedby="troca-plano-dialog-desc"
        slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}
      >
        <DialogTitle>
          {etapa === "confirmando" && "Confirmar troca de plano"}
          {etapa === "pagando" && "Pagamento"}
          {etapa === "sucesso" && "Troca concluída"}
        </DialogTitle>
        <DialogContent id="troca-plano-dialog-desc">
          {etapa === "confirmando" && planoSelecionado && (
            <Stack spacing={2} sx={{ mt: 1 }}>
              {erro && <Alert severity="error">{erro}</Alert>}
              <Typography variant="body2">
                {planoAtual && planoSelecionado.preco > planoAtual.preco
                  ? `Upgrade para ${planoSelecionado.nome}. O valor exato de proração será confirmado após processar.`
                  : `Downgrade para ${planoSelecionado.nome}. O plano muda na próxima renovação.`}
              </Typography>
              <Divider />
              <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end" }}>
                <Button onClick={fecharDialog} disabled={processando}>Cancelar</Button>
                <Button
                  variant="contained"
                  onClick={confirmarTroca}
                  disabled={processando}
                >
                  {processando ? <CircularProgress size={20} /> : "Confirmar"}
                </Button>
              </Stack>
            </Stack>
          )}

          {etapa === "pagando" && trocaResp && (
            <Stack spacing={2} sx={{ mt: 1 }}>
              {trocaResp.valorPagamento !== null && trocaResp.valorPagamento !== undefined && (
                <Typography variant="body2" color="primary.main" sx={{ fontWeight: "bold" }}>
                  Valor: {formatarBRL(trocaResp.valorPagamento)}
                </Typography>
              )}
              {trocaResp.pixQrCode && (
                <>
                  <Typography variant="body2">
                    Escaneie o QR code abaixo para confirmar o pagamento.
                    A troca será aplicada automaticamente após a confirmação.
                  </Typography>
                  <Box sx={{ textAlign: "center", my: 1 }}>
                    {trocaResp.pixQrCodeUrl && (
                      <Box
                        component="img"
                        src={trocaResp.pixQrCodeUrl}
                        alt="QR Code Pix"
                        sx={{ maxWidth: 200 }}
                      />
                    )}
                    <Typography variant="caption" sx={{ display: "block", wordBreak: "break-all", mt: 1 }}>
                      {trocaResp.pixQrCode}
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
              {trocaResp?.tipo === "Downgrade" ? (
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
          {erroCancelar && <Alert severity="error">{erroCancelar}</Alert>}
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
