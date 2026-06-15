"use client";
import { useEffect, useState } from "react";
import {
  Box, Typography, Card, CardContent, Stack, TextField, Button, Divider, Chip, Avatar,
  Dialog, DialogTitle, DialogContent, DialogActions, Autocomplete,
} from "@mui/material";
import PersonIcon from "@mui/icons-material/Person";
import LockIcon from "@mui/icons-material/Lock";
import SwapHorizIcon from "@mui/icons-material/SwapHoriz";
import FitnessCenterIcon from "@mui/icons-material/FitnessCenter";
import SecurityIcon from "@mui/icons-material/Security";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import ConfirmDialog from "@/components/ui/ConfirmDialog";
import ConsentBanner from "@/components/ui/ConsentBanner";
import { contaApi, type PerfilResponse } from "@/lib/api/conta";
import { baixarMeusDados } from "@/lib/utils/downloadBlob";
import { alunoApi } from "@/lib/api/aluno";
import { apiClient } from "@/lib/api/client";
import { extractApiError } from "@/lib/api/extractApiError";
import { useAuth } from "@/lib/auth/context";
import type { MeuVinculoResponse, TreinadorResponse, PacoteResponse } from "@/types";

export default function PerfilPage() {
  const { logout } = useAuth();
  const [perfil, setPerfil] = useState<PerfilResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [nome, setNome] = useState("");
  const [savingPerfil, setSavingPerfil] = useState(false);

  const [senhaAtual, setSenhaAtual] = useState("");
  const [novaSenha, setNovaSenha] = useState("");
  const [confirmarSenha, setConfirmarSenha] = useState("");
  const [savingSenha, setSavingSenha] = useState(false);
  const [senhaError, setSenhaError] = useState("");

  const [meuVinculo, setMeuVinculo] = useState<MeuVinculoResponse | null>(null);
  const [trocaDialog, setTrocaDialog] = useState(false);
  const [treinadores, setTreinadores] = useState<TreinadorResponse[]>([]);
  const [selectedTreinador, setSelectedTreinador] = useState<TreinadorResponse | null>(null);
  const [pacotesTroca, setPacotesTroca] = useState<PacoteResponse[]>([]);
  const [selectedPacoteTroca, setSelectedPacoteTroca] = useState<PacoteResponse | null>(null);
  const [loadingTrocaPacotes, setLoadingTrocaPacotes] = useState(false);
  const [savingTroca, setSavingTroca] = useState(false);

  const [exportingData, setExportingData] = useState<false | "xlsx" | "json">(false);
  const [deleteAccountDialog, setDeleteAccountDialog] = useState(false);
  const [deleteSenha, setDeleteSenha] = useState("");
  const [deletingAccount, setDeletingAccount] = useState(false);
  const [consentBannerOpen, setConsentBannerOpen] = useState(false);

  useEffect(() => {
    const load = async () => {
      try {
        const res = await contaApi.getPerfil();
        setPerfil(res.data);
        setNome(res.data.nome);
        if (res.data.tipoConta === "Aluno") {
          const vinculoRes = await alunoApi.getMeuVinculo();
          setMeuVinculo(vinculoRes.data);
        }
      } catch {
        setError("Erro ao carregar perfil.");
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
      } catch {
        setError("Erro ao carregar treinadores.");
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
    } catch {
      setError("Erro ao carregar pacotes.");
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
    } catch {
      setError("Erro ao solicitar troca de treinador.");
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

  const handleExcluirConta = async () => {
    if (!deleteSenha.trim()) return;
    setDeletingAccount(true);
    setError("");
    try {
      await contaApi.excluirConta(deleteSenha);
      setDeleteAccountDialog(false);
      await logout();
    } catch (err) {
      setError(extractApiError(err, "Erro ao excluir conta. Verifique sua senha."));
      setDeleteAccountDialog(false);
    } finally {
      setDeletingAccount(false);
      setDeleteSenha("");
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
    } catch {
      setError("Erro ao atualizar perfil.");
    } finally {
      setSavingPerfil(false);
    }
  };

  const handleAlterarSenha = async () => {
    setSenhaError("");
    if (novaSenha !== confirmarSenha) { setSenhaError("As senhas não coincidem."); return; }
    if (novaSenha.length < 8) { setSenhaError("A nova senha deve ter pelo menos 8 caracteres."); return; }
    setSavingSenha(true);
    setError("");
    try {
      await contaApi.alterarSenha({ senhaAtual, novaSenha });
      setSuccess("Senha alterada com sucesso.");
      setSenhaAtual(""); setNovaSenha(""); setConfirmarSenha("");
    } catch {
      setError("Erro ao alterar senha. Verifique a senha atual.");
    } finally {
      setSavingSenha(false);
    }
  };

  if (loading) return <LoadingSpinner />;

  return (
    <Box sx={{ maxWidth: { xs: "100%", md: 580 } }}>
      <Box sx={{ mb: 4 }}>
        <Typography variant="h5" sx={{ fontWeight: 700 }}>Meu Perfil</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
          Informações da conta e configurações de acesso
        </Typography>
      </Box>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!success} severity="success" message={success} onClose={() => setSuccess("")} />

      {/* Dados da conta */}
      <Card sx={{ mb: 2.5, border: "1px solid", borderColor: "divider" }}>
        <CardContent sx={{ p: 3, "&:last-child": { pb: 3 } }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 3 }}>
            <Box sx={{ width: 36, height: 36, borderRadius: 2, bgcolor: "rgba(26,26,26,0.06)", display: "flex", alignItems: "center", justifyContent: "center" }}>
              <PersonIcon fontSize="small" sx={{ color: "text.secondary" }} />
            </Box>
            <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>Dados da conta</Typography>
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

      {/* Meu Treinador — apenas para Alunos */}
      {perfil?.tipoConta === "Aluno" && (
        <Card sx={{ mb: 2.5, border: "1px solid", borderColor: "divider" }}>
          <CardContent sx={{ p: 3, "&:last-child": { pb: 3 } }}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 3 }}>
              <Box sx={{ width: 36, height: 36, borderRadius: 2, bgcolor: "rgba(26,26,26,0.06)", display: "flex", alignItems: "center", justifyContent: "center" }}>
                <FitnessCenterIcon fontSize="small" sx={{ color: "text.secondary" }} />
              </Box>
              <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>Meu Treinador</Typography>
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
              <Box sx={{ mt: 2, p: 2, bgcolor: "rgba(255,193,7,0.08)", borderRadius: 2, border: "1px solid", borderColor: "warning.light" }}>
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

      {/* Alterar senha */}
      <Card sx={{ border: "1px solid", borderColor: "divider" }}>
        <CardContent sx={{ p: 3, "&:last-child": { pb: 3 } }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 3 }}>
            <Box sx={{ width: 36, height: 36, borderRadius: 2, bgcolor: "rgba(211,47,47,0.08)", display: "flex", alignItems: "center", justifyContent: "center" }}>
              <LockIcon fontSize="small" sx={{ color: "error.main" }} />
            </Box>
            <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>Alterar senha</Typography>
          </Box>

          {senhaError && (
            <AlertBanner open severity="error" message={senhaError} onClose={() => setSenhaError("")} />
          )}

          <Stack spacing={2}>
            <TextField
              label="Senha atual"
              type="password"
              value={senhaAtual}
              onChange={(e) => setSenhaAtual(e.target.value)}
              size="small"
              fullWidth
              autoComplete="current-password"
            />
            <Divider />
            <TextField
              label="Nova senha"
              type="password"
              value={novaSenha}
              onChange={(e) => setNovaSenha(e.target.value)}
              size="small"
              fullWidth
              autoComplete="new-password"
            />
            <TextField
              label="Confirmar nova senha"
              type="password"
              value={confirmarSenha}
              onChange={(e) => setConfirmarSenha(e.target.value)}
              size="small"
              fullWidth
              autoComplete="new-password"
            />
            <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
              <Button
                variant="contained"
                disabled={!senhaAtual || !novaSenha || !confirmarSenha || savingSenha}
                onClick={handleAlterarSenha}
              >
                {savingSenha ? "Alterando..." : "Alterar senha"}
              </Button>
            </Box>
          </Stack>
        </CardContent>
      </Card>
      {/* Privacidade (LGPD) */}
      <Card sx={{ mt: 2.5, border: "1px solid", borderColor: "divider" }}>
        <CardContent sx={{ p: 3, "&:last-child": { pb: 3 } }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 3 }}>
            <Box sx={{ width: 36, height: 36, borderRadius: 2, bgcolor: "rgba(26,26,26,0.06)", display: "flex", alignItems: "center", justifyContent: "center" }}>
              <SecurityIcon fontSize="small" sx={{ color: "text.secondary" }} />
            </Box>
            <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>Privacidade (LGPD)</Typography>
          </Box>
          <Stack spacing={1.5}>
            <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap" }}>
              <Button
                variant="outlined"
                size="small"
                disabled={!!exportingData}
                onClick={() => handleExportarDados("xlsx")}
                sx={{ alignSelf: "flex-start" }}
              >
                {exportingData === "xlsx" ? "Exportando..." : "Baixar meus dados (Excel)"}
              </Button>
              <Button
                variant="text"
                size="small"
                disabled={!!exportingData}
                onClick={() => handleExportarDados("json")}
                sx={{ alignSelf: "flex-start" }}
              >
                {exportingData === "json" ? "Exportando..." : "Baixar como JSON"}
              </Button>
            </Stack>
            <Button
              variant="outlined"
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
              onClick={() => { setDeleteSenha(""); setDeleteAccountDialog(true); }}
              sx={{ alignSelf: "flex-start" }}
            >
              Excluir minha conta
            </Button>
          </Stack>
        </CardContent>
      </Card>

      {/* LGPD: confirm delete account */}
      <ConfirmDialog
        open={deleteAccountDialog}
        title="Excluir minha conta"
        description="Esta ação é irreversível. Todos os seus dados serão excluídos permanentemente. Digite sua senha para confirmar."
        confirmLabel={deletingAccount ? "Excluindo..." : "Excluir conta"}
        destructive
        loading={deletingAccount}
        onConfirm={handleExcluirConta}
        onClose={() => { setDeleteAccountDialog(false); setDeleteSenha(""); }}
      >
        <TextField
          label="Senha"
          type="password"
          value={deleteSenha}
          onChange={(e) => setDeleteSenha(e.target.value)}
          size="small"
          fullWidth
          sx={{ mt: 2 }}
          autoComplete="current-password"
        />
      </ConfirmDialog>

      {/* LGPD: reopen consent preferences */}
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
