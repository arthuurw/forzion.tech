"use client";
import { useEffect, useState } from "react";
import {
  Box, Typography, Card, CardContent, Stack, TextField, Button, Divider, Chip, Avatar,
  Dialog, DialogTitle, DialogContent, DialogActions, Autocomplete, FormControlLabel, Switch,
} from "@mui/material";
import { alpha } from "@mui/material/styles";
import Link from "next/link";
import { useForm, FormProvider } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import PersonIcon from "@mui/icons-material/Person";
import AssignmentIcon from "@mui/icons-material/Assignment";
import LockIcon from "@mui/icons-material/Lock";
import SwapHorizIcon from "@mui/icons-material/SwapHoriz";
import FitnessCenterIcon from "@mui/icons-material/FitnessCenter";
import SecurityIcon from "@mui/icons-material/Security";
import NotificationsIcon from "@mui/icons-material/Notifications";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import ConsentBanner from "@/components/ui/ConsentBanner";
import PageHeader from "@/components/ui/PageHeader";
import PasswordField from "@/components/forms/PasswordField";
import { contaApi, type PerfilResponse } from "@/lib/api/conta";
import { baixarMeusDados } from "@/lib/utils/downloadBlob";
import { alunoApi } from "@/lib/api/aluno";
import { apiClient } from "@/lib/api/client";
import { extractApiError } from "@/lib/api/extractApiError";
import { useAuth } from "@/lib/auth/context";
import type { MeuVinculoResponse, TreinadorResponse, PacoteResponse } from "@/types";

const senhaSchema = z
  .object({
    senhaAtual: z.string().min(1, "Informe a senha atual."),
    novaSenha: z.string().min(8, "A nova senha deve ter pelo menos 8 caracteres."),
    confirmarSenha: z.string().min(1, "Confirme a nova senha."),
  })
  .refine((d) => d.novaSenha === d.confirmarSenha, {
    message: "As senhas não coincidem.",
    path: ["confirmarSenha"],
  });
type SenhaForm = z.infer<typeof senhaSchema>;

const excluirContaSchema = z.object({ senha: z.string().min(1, "Informe sua senha.") });
type ExcluirContaForm = z.infer<typeof excluirContaSchema>;

export default function PerfilPage() {
  const { logout } = useAuth();
  const [perfil, setPerfil] = useState<PerfilResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [nome, setNome] = useState("");
  const [savingPerfil, setSavingPerfil] = useState(false);

  const [savingSenha, setSavingSenha] = useState(false);
  const senhaForm = useForm<SenhaForm>({
    resolver: zodResolver(senhaSchema),
    defaultValues: { senhaAtual: "", novaSenha: "", confirmarSenha: "" },
  });
  const excluirContaForm = useForm<ExcluirContaForm>({
    resolver: zodResolver(excluirContaSchema),
    defaultValues: { senha: "" },
  });

  const [meuVinculo, setMeuVinculo] = useState<MeuVinculoResponse | null>(null);
  const [trocaDialog, setTrocaDialog] = useState(false);
  const [treinadores, setTreinadores] = useState<TreinadorResponse[]>([]);
  const [selectedTreinador, setSelectedTreinador] = useState<TreinadorResponse | null>(null);
  const [pacotesTroca, setPacotesTroca] = useState<PacoteResponse[]>([]);
  const [selectedPacoteTroca, setSelectedPacoteTroca] = useState<PacoteResponse | null>(null);
  const [loadingTrocaPacotes, setLoadingTrocaPacotes] = useState(false);
  const [savingTroca, setSavingTroca] = useState(false);

  const [receberEngajamento, setReceberEngajamento] = useState(true);
  const [savingPreferencia, setSavingPreferencia] = useState(false);

  const [exportingData, setExportingData] = useState<false | "xlsx" | "json">(false);
  const [deleteAccountDialog, setDeleteAccountDialog] = useState(false);
  const [deletingAccount, setDeletingAccount] = useState(false);
  const [consentBannerOpen, setConsentBannerOpen] = useState(false);

  useEffect(() => {
    const load = async () => {
      try {
        const res = await contaApi.getPerfil();
        setPerfil(res.data);
        setNome(res.data.nome);
        setReceberEngajamento(!res.data.emailEngajamentoOptOut);
        if (res.data.tipoConta === "Aluno") {
          const vinculoRes = await alunoApi.getMeuVinculo();
          setMeuVinculo(vinculoRes.data);
        }
      } catch (err) {
        setError(extractApiError(err, "Erro ao carregar perfil."));
      } finally {
        setLoading(false);
      }
    };
    load();
  }, []);

  const openTrocaDialog = async () => {
    setTrocaDialog(true);
    setSelectedTreinador(null);
    setSelectedPacoteTroca(null);
    setPacotesTroca([]);
    if (treinadores.length === 0) {
      try {
        const res = await apiClient.get<TreinadorResponse[]>("/auth/treinadores");
        setTreinadores(res.data.filter((t) => t.treinadorId !== meuVinculo?.vinculoAtivo?.treinadorId));
      } catch (err) {
        setError(extractApiError(err, "Erro ao carregar treinadores."));
      }
    }
  };

  const handleSelectTreinadorTroca = async (treinador: TreinadorResponse | null) => {
    setSelectedTreinador(treinador);
    setSelectedPacoteTroca(null);
    setPacotesTroca([]);
    if (!treinador) return;
    setLoadingTrocaPacotes(true);
    try {
      const res = await apiClient.get<PacoteResponse[]>(`/auth/treinadores/${treinador.treinadorId}/pacotes`);
      setPacotesTroca(res.data);
    } catch (err) {
      setError(extractApiError(err, "Erro ao carregar pacotes."));
    } finally {
      setLoadingTrocaPacotes(false);
    }
  };

  const handleSolicitarTroca = async () => {
    if (!selectedTreinador || !selectedPacoteTroca) return;
    setSavingTroca(true);
    try {
      await alunoApi.solicitarTrocaTreinador(selectedTreinador.treinadorId, selectedPacoteTroca.pacoteId);
      setSuccess(`Solicitação enviada. Aguarde aprovação de ${selectedTreinador.nome}.`);
      setTrocaDialog(false);
      const vinculoRes = await alunoApi.getMeuVinculo();
      setMeuVinculo(vinculoRes.data);
    } catch (err) {
      setError(extractApiError(err, "Erro ao solicitar troca de treinador."));
    } finally {
      setSavingTroca(false);
    }
  };

  const handleExportarDados = async (formato: "xlsx" | "json") => {
    setExportingData(formato);
    setError("");
    try {
      await baixarMeusDados(formato);
    } catch (err) {
      setError(extractApiError(err, "Erro ao exportar dados."));
    } finally {
      setExportingData(false);
    }
  };

  const handleExcluirConta = excluirContaForm.handleSubmit(async ({ senha }) => {
    setDeletingAccount(true);
    setError("");
    try {
      await contaApi.excluirConta(senha);
      setDeleteAccountDialog(false);
      await logout();
    } catch (err) {
      setError(extractApiError(err, "Erro ao excluir conta. Verifique sua senha."));
      setDeleteAccountDialog(false);
    } finally {
      setDeletingAccount(false);
      excluirContaForm.reset();
    }
  });

  const handleToggleEngajamento = async (receber: boolean) => {
    setReceberEngajamento(receber);
    setSavingPreferencia(true);
    setError("");
    try {
      await contaApi.atualizarPreferenciasNotificacao({ emailEngajamentoOptOut: !receber });
      setSuccess(receber ? "E-mails de engajamento ativados." : "E-mails de engajamento desativados.");
    } catch (err) {
      setReceberEngajamento(!receber);
      setError(extractApiError(err, "Erro ao salvar preferência de notificações."));
    } finally {
      setSavingPreferencia(false);
    }
  };

  const handleSalvarPerfil = async () => {
    if (!nome.trim()) return;
    setSavingPerfil(true);
    setError("");
    try {
      await contaApi.atualizarPerfil({ nome: nome.trim() });
      setSuccess("Perfil atualizado com sucesso.");
      setPerfil((current) => current ? { ...current, nome: nome.trim() } : current);
    } catch (err) {
      setError(extractApiError(err, "Erro ao atualizar perfil."));
    } finally {
      setSavingPerfil(false);
    }
  };

  const handleAlterarSenha = senhaForm.handleSubmit(async ({ senhaAtual, novaSenha }) => {
    setSavingSenha(true);
    setError("");
    try {
      await contaApi.alterarSenha({ senhaAtual, novaSenha });
      setSuccess("Senha alterada com sucesso.");
      senhaForm.reset();
    } catch (err) {
      setError(extractApiError(err, "Erro ao alterar senha. Verifique a senha atual."));
    } finally {
      setSavingSenha(false);
    }
  });

  if (loading) return <LoadingSpinner />;

  return (
    <Box sx={{ maxWidth: { xs: "100%", md: 580 } }}>
      <PageHeader title="Meu Perfil" subtitle="Informações da conta e configurações de acesso" />

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!success} severity="success" message={success} onClose={() => setSuccess("")} />

      <Card sx={{ mb: 2.5, border: "1px solid", borderColor: "divider" }}>
        <CardContent sx={{ p: 3, "&:last-child": { pb: 3 } }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 3 }}>
            <Box sx={{ width: 36, height: 36, borderRadius: 2, bgcolor: "action.subtleBg", display: "flex", alignItems: "center", justifyContent: "center" }}>
              <PersonIcon fontSize="small" sx={{ color: "text.secondary" }} />
            </Box>
            <Typography variant="subtitle1" component="h2">Dados da conta</Typography>
          </Box>

          <Box sx={{ display: "flex", alignItems: "center", gap: 2, mb: 3, p: 2, bgcolor: "background.default", borderRadius: 2, flexWrap: "wrap" }}>
            <Avatar sx={{ width: 44, height: 44, bgcolor: "secondary.main", fontWeight: 700, fontSize: 16 }}>
              {perfil?.nome?.[0]?.toUpperCase() ?? "?"}
            </Avatar>
            <Box sx={{ minWidth: 0 }}>
              <Typography variant="body2" sx={{ fontWeight: 600, overflowWrap: "anywhere" }}>{perfil?.nome}</Typography>
              <Typography variant="caption" color="text.secondary" sx={{ overflowWrap: "anywhere" }}>{perfil?.email}</Typography>
            </Box>
            <Box sx={{ ml: "auto" }}>
              <Chip label={perfil?.tipoConta} size="small" sx={{ bgcolor: "primary.main", color: "secondary.main", fontWeight: 700 }} />
            </Box>
          </Box>

          <Stack spacing={2}>
            <TextField
              label="E-mail"
              value={perfil?.email ?? ""}
              size="small"
              fullWidth
              disabled
            />
            <TextField
              label="Nome"
              value={nome}
              onChange={(e) => setNome(e.target.value)}
              size="small"
              fullWidth
            />
            <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
              <Button
                variant="contained"
                disabled={!nome.trim() || savingPerfil || nome === perfil?.nome}
                onClick={handleSalvarPerfil}
              >
                {savingPerfil ? "Salvando..." : "Salvar alterações"}
              </Button>
            </Box>
          </Stack>
        </CardContent>
      </Card>

      {perfil?.tipoConta === "Aluno" && (
        <Card sx={{ mb: 2.5, border: "1px solid", borderColor: "divider" }}>
          <CardContent sx={{ p: 3, "&:last-child": { pb: 3 } }}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 3 }}>
              <Box sx={{ width: 36, height: 36, borderRadius: 2, bgcolor: "action.subtleBg", display: "flex", alignItems: "center", justifyContent: "center" }}>
                <FitnessCenterIcon fontSize="small" sx={{ color: "text.secondary" }} />
              </Box>
              <Typography variant="subtitle1" component="h2">Meu Treinador</Typography>
            </Box>

            {meuVinculo?.vinculoAtivo ? (
              <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", p: 2, bgcolor: "background.default", borderRadius: 2, flexWrap: "wrap", gap: 1 }}>
                <Box>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>{meuVinculo.vinculoAtivo.nomeTreinador}</Typography>
                  {meuVinculo.vinculoAtivo.dataInicio && (
                    <Typography variant="caption" color="text.secondary">
                      Desde {new Date(meuVinculo.vinculoAtivo.dataInicio).toLocaleDateString("pt-BR")}
                    </Typography>
                  )}
                </Box>
                <Button
                  size="small"
                  variant="outlined"
                  startIcon={<SwapHorizIcon />}
                  onClick={openTrocaDialog}
                  disabled={!!meuVinculo.vinculoPendente}
                >
                  Solicitar troca
                </Button>
              </Box>
            ) : (
              <Typography variant="body2" color="text.secondary">
                Nenhum vínculo ativo no momento.
              </Typography>
            )}

            {meuVinculo?.vinculoPendente && (
              <Box sx={(theme) => ({ mt: 2, p: 2, bgcolor: alpha(theme.palette.warning.main, 0.08), borderRadius: 2, border: "1px solid", borderColor: "warning.light" })}>
                <Typography variant="caption" color="warning.dark" sx={{ fontWeight: 600 }}>
                  Solicitação de troca pendente
                </Typography>
                <Typography variant="body2" sx={{ mt: 0.5 }}>
                  Aguardando aprovação de <strong>{meuVinculo.vinculoPendente.nomeTreinador}</strong>
                </Typography>
              </Box>
            )}
          </CardContent>
        </Card>
      )}

      {perfil?.tipoConta === "Aluno" && (
        <Card sx={{ mb: 2.5, border: "1px solid", borderColor: "divider" }}>
          <CardContent sx={{ p: 3, "&:last-child": { pb: 3 } }}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 2 }}>
              <Box sx={{ width: 36, height: 36, borderRadius: 2, bgcolor: "action.subtleBg", display: "flex", alignItems: "center", justifyContent: "center" }}>
                <AssignmentIcon fontSize="small" sx={{ color: "text.secondary" }} />
              </Box>
              <Typography variant="subtitle1" component="h2">Minha anamnese</Typography>
            </Box>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
              Atualize disponibilidade, objetivos e informações de saúde que orientam o seu treino.
            </Typography>
            <Button component={Link} href="/perfil/anamnese" variant="outlined" size="small">
              Editar anamnese
            </Button>
          </CardContent>
        </Card>
      )}

      <Card sx={{ border: "1px solid", borderColor: "divider" }}>
        <CardContent sx={{ p: 3, "&:last-child": { pb: 3 } }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 3 }}>
            <Box sx={(theme) => ({ width: 36, height: 36, borderRadius: 2, bgcolor: alpha(theme.palette.error.main, 0.08), display: "flex", alignItems: "center", justifyContent: "center" })}>
              <LockIcon fontSize="small" sx={{ color: "error.main" }} />
            </Box>
            <Typography variant="subtitle1" component="h2">Alterar senha</Typography>
          </Box>

          <FormProvider {...senhaForm}>
            <Stack component="form" spacing={2} onSubmit={handleAlterarSenha} noValidate>
              <PasswordField
                name="senhaAtual"
                label="Senha atual"
                size="small"
                fullWidth
                autoComplete="current-password"
              />
              <Divider />
              <PasswordField
                name="novaSenha"
                label="Nova senha"
                size="small"
                fullWidth
                autoComplete="new-password"
              />
              <PasswordField
                name="confirmarSenha"
                label="Confirmar nova senha"
                size="small"
                fullWidth
                autoComplete="new-password"
              />
              <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
                <Button type="submit" variant="contained" disabled={savingSenha}>
                  {savingSenha ? "Alterando..." : "Alterar senha"}
                </Button>
              </Box>
            </Stack>
          </FormProvider>
        </CardContent>
      </Card>
      <Card sx={{ mt: 2.5, border: "1px solid", borderColor: "divider" }}>
        <CardContent sx={{ p: 3, "&:last-child": { pb: 3 } }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 2 }}>
            <Box sx={{ width: 36, height: 36, borderRadius: 2, bgcolor: "action.subtleBg", display: "flex", alignItems: "center", justifyContent: "center" }}>
              <NotificationsIcon fontSize="small" sx={{ color: "text.secondary" }} />
            </Box>
            <Typography variant="subtitle1" component="h2">Notificações</Typography>
          </Box>
          <FormControlLabel
            sx={{ ml: 0, gap: 1 }}
            control={
              <Switch
                checked={receberEngajamento}
                onChange={(e) => handleToggleEngajamento(e.target.checked)}
                disabled={savingPreferencia}
              />
            }
            label="Receber e-mails de engajamento"
          />
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            Novidades de treino, lembretes e conquistas. E-mails de cobrança e da sua conta continuam chegando normalmente.
          </Typography>
        </CardContent>
      </Card>

      <Card sx={{ mt: 2.5, border: "1px solid", borderColor: "divider" }}>
        <CardContent sx={{ p: 3, "&:last-child": { pb: 3 } }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 3 }}>
            <Box sx={{ width: 36, height: 36, borderRadius: 2, bgcolor: "action.subtleBg", display: "flex", alignItems: "center", justifyContent: "center" }}>
              <SecurityIcon fontSize="small" sx={{ color: "text.secondary" }} />
            </Box>
            <Typography variant="subtitle1" component="h2">Privacidade (LGPD)</Typography>
          </Box>
          <Stack spacing={1.5}>
            <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap" }}>
              <Button
                variant="contained"
                size="small"
                disabled={!!exportingData}
                onClick={() => handleExportarDados("xlsx")}
                sx={{ alignSelf: "flex-start" }}
              >
                {exportingData === "xlsx" ? "Exportando..." : "Baixar meus dados (Excel)"}
              </Button>
              <Button
                variant="contained"
                size="small"
                disabled={!!exportingData}
                onClick={() => handleExportarDados("json")}
                sx={{ alignSelf: "flex-start" }}
              >
                {exportingData === "json" ? "Exportando..." : "Baixar como JSON"}
              </Button>
            </Stack>
            <Button
              variant="contained"
              size="small"
              onClick={() => setConsentBannerOpen(true)}
              sx={{ alignSelf: "flex-start" }}
            >
              Preferências de cookies
            </Button>
            <Divider />
            <Button
              variant="outlined"
              color="error"
              size="small"
              onClick={() => { excluirContaForm.reset(); setDeleteAccountDialog(true); }}
              sx={{ alignSelf: "flex-start" }}
            >
              Excluir minha conta
            </Button>
          </Stack>
        </CardContent>
      </Card>

      <ConfirmDialog
        open={deleteAccountDialog}
        title="Excluir minha conta"
        description="Esta ação é irreversível. Todos os seus dados serão excluídos permanentemente. Digite sua senha para confirmar."
        confirmLabel={deletingAccount ? "Excluindo..." : "Excluir conta"}
        destructive
        loading={deletingAccount}
        onConfirm={handleExcluirConta}
        onClose={() => { setDeleteAccountDialog(false); excluirContaForm.reset(); }}
      >
        <FormProvider {...excluirContaForm}>
          <Box component="form" onSubmit={handleExcluirConta}>
            <PasswordField
              name="senha"
              label="Senha"
              size="small"
              fullWidth
              sx={{ mt: 2 }}
              autoComplete="current-password"
            />
          </Box>
        </FormProvider>
      </ConfirmDialog>

      {consentBannerOpen && (
        <ConsentBanner forceOpen onClose={() => setConsentBannerOpen(false)} />
      )}

      <Dialog open={trocaDialog} onClose={() => setTrocaDialog(false)} maxWidth="xs" fullWidth slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}>
        <DialogTitle>Solicitar troca de treinador</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <Autocomplete
              options={treinadores}
              getOptionLabel={(t) => t.nome}
              value={selectedTreinador}
              onChange={(_, v) => handleSelectTreinadorTroca(v)}
              renderInput={(params) => <TextField {...params} label="Novo treinador" size="small" />}
            />
            <Autocomplete
              options={pacotesTroca}
              getOptionLabel={(p) => p.descricao ? `${p.nome} — ${p.descricao}` : p.nome}
              value={selectedPacoteTroca}
              onChange={(_, v) => setSelectedPacoteTroca(v)}
              disabled={!selectedTreinador || loadingTrocaPacotes}
              loading={loadingTrocaPacotes}
              renderInput={(params) => <TextField {...params} label="Pacote" size="small" />}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setTrocaDialog(false)}>Cancelar</Button>
          <Button
            variant="contained"
            disabled={!selectedTreinador || !selectedPacoteTroca || savingTroca}
            onClick={handleSolicitarTroca}
          >
            Solicitar
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
