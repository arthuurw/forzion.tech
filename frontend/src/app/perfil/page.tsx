"use client";
import { useEffect, useState } from "react";
import {
  Box, Typography, Card, CardContent, Stack, TextField, Button, Divider, Chip,
} from "@mui/material";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import { contaApi, type PerfilResponse } from "@/lib/api/conta";

export default function PerfilPage() {
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

  useEffect(() => {
    const load = async () => {
      try {
        const res = await contaApi.getPerfil();
        setPerfil(res.data);
        setNome(res.data.nome);
      } catch {
        setError("Erro ao carregar perfil.");
      } finally {
        setLoading(false);
      }
    };
    load();
  }, []);

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
    if (novaSenha !== confirmarSenha) {
      setSenhaError("As senhas nao coincidem.");
      return;
    }
    if (novaSenha.length < 8) {
      setSenhaError("A nova senha deve ter pelo menos 8 caracteres.");
      return;
    }
    setSavingSenha(true);
    setError("");
    try {
      await contaApi.alterarSenha({ senhaAtual, novaSenha });
      setSuccess("Senha alterada com sucesso.");
      setSenhaAtual("");
      setNovaSenha("");
      setConfirmarSenha("");
    } catch {
      setError("Erro ao alterar senha. Verifique a senha atual.");
    } finally {
      setSavingSenha(false);
    }
  };

  if (loading) return <LoadingSpinner />;

  return (
    <Box sx={{ maxWidth: 600 }}>
      <Typography variant="h5" sx={{ fontWeight: 700, mb: 3 }}>Meu Perfil</Typography>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!success} severity="success" message={success} onClose={() => setSuccess("")} />

      <Card variant="outlined" sx={{ mb: 3 }}>
        <CardContent>
          <Typography variant="h6" sx={{ fontWeight: 600, mb: 2 }}>Dados da conta</Typography>
          <Stack spacing={2}>
            <Box sx={{ display: "flex", justifyContent: "flex-start" }}>
              <Chip label={perfil?.tipoConta ?? "Conta"} size="small" variant="outlined" />
            </Box>
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
                {savingPerfil ? "Salvando..." : "Salvar"}
              </Button>
            </Box>
          </Stack>
        </CardContent>
      </Card>

      <Card variant="outlined">
        <CardContent>
          <Typography variant="h6" sx={{ fontWeight: 600, mb: 2 }}>Alterar senha</Typography>
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
    </Box>
  );
}
