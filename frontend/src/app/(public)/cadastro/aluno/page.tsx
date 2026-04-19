"use client";
import { useState } from "react";
import {
  Box,
  Typography,
  Button,
  CircularProgress,
  Paper,
  Stepper,
  Step,
  StepLabel,
  Stack,
  Card,
  CardActionArea,
  CardContent,
  Radio,
} from "@mui/material";
import { useForm, FormProvider } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircle";
import Link from "next/link";
import FormTextField from "@/components/forms/FormTextField";
import PasswordField from "@/components/forms/PasswordField";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import { cadastroAlunoSchema, type CadastroAlunoFormData } from "@/lib/validations/common";
import type { ProblemDetails, TreinadorResponse, PacoteAlunoResponse } from "@/types";

const STEPS = ["Escolher treinador", "Escolher pacote", "Seus dados"];

export default function CadastroAlunoPage() {
  const [activeStep, setActiveStep] = useState(0);
  const [treinadores, setTreinadores] = useState<TreinadorResponse[]>([]);
  const [pacotes, setPacotes] = useState<PacoteAlunoResponse[]>([]);
  const [selectedTreinador, setSelectedTreinador] = useState<TreinadorResponse | null>(null);
  const [selectedPacote, setSelectedPacote] = useState<PacoteAlunoResponse | null>(null);
  const [loadingList, setLoadingList] = useState(false);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(false);

  const methods = useForm<CadastroAlunoFormData>({
    resolver: zodResolver(cadastroAlunoSchema),
    defaultValues: { nome: "", email: "", telefone: "", password: "", confirmPassword: "" },
  });

  const fetchTreinadores = async () => {
    setLoadingList(true);
    setError("");
    try {
      const res = await fetch("/api/auth/treinadores");
      if (!res.ok) throw new Error();
      setTreinadores(await res.json());
    } catch {
      setError("Nao foi possivel carregar os treinadores.");
    } finally {
      setLoadingList(false);
    }
  };

  const fetchPacotes = async (treinadorId: string) => {
    setLoadingList(true);
    setError("");
    try {
      const res = await fetch(`/api/auth/treinadores/${treinadorId}/pacotes`);
      if (!res.ok) throw new Error();
      setPacotes(await res.json());
    } catch {
      setError("Nao foi possivel carregar os pacotes.");
    } finally {
      setLoadingList(false);
    }
  };

  const goToStep = (step: number) => {
    setError("");
    if (step === 0 && treinadores.length === 0) fetchTreinadores();
    setActiveStep(step);
  };

  const handleSelectTreinador = (treinador: TreinadorResponse) => {
    setSelectedTreinador(treinador);
    setSelectedPacote(null);
    setPacotes([]);
    fetchPacotes(treinador.treinadorId);
    setActiveStep(1);
  };

  const handleSelectPacote = (pacote: PacoteAlunoResponse) => {
    setSelectedPacote(pacote);
    setActiveStep(2);
  };

  const onSubmit = async (data: CadastroAlunoFormData) => {
    if (!selectedTreinador || !selectedPacote) return;
    setError("");
    setLoading(true);
    try {
      const res = await fetch("/api/auth/register/aluno", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          nome: data.nome,
          email: data.email,
          telefone: data.telefone || null,
          senha: data.password,
          treinadorId: selectedTreinador.treinadorId,
          pacoteId: selectedPacote.pacoteId,
        }),
      });

      if (!res.ok) {
        const problem: ProblemDetails = await res.json();
        setError(problem.detail ?? problem.title ?? "Erro ao criar conta.");
        return;
      }

      setSuccess(true);
    } catch {
      setError("Nao foi possivel conectar ao servidor.");
    } finally {
      setLoading(false);
    }
  };

  if (success) {
    return (
      <Paper elevation={0} variant="outlined" sx={{ p: 4, textAlign: "center" }}>
        <CheckCircleOutlineIcon sx={{ fontSize: 56, color: "primary.main", mb: 2 }} />
        <Typography variant="h6" sx={{ fontWeight: 700, mb: 1 }}>
          Cadastro enviado!
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
          Seu vinculo com <strong>{selectedTreinador?.nome}</strong> aguarda aprovacao.
          Voce ja pode entrar na plataforma.
        </Typography>
        <Link href="/login" style={{ textDecoration: "none" }}>
          <Button variant="contained" color="primary">Ir para o login</Button>
        </Link>
      </Paper>
    );
  }

  return (
    <Box>
      <Typography variant="h5" sx={{ fontWeight: 700, mb: 0.5 }}>
        Criar conta como aluno
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        Escolha seu treinador e pacote para comecar.
      </Typography>

      <Stepper activeStep={activeStep} sx={{ mb: 4 }}>
        {STEPS.map((label) => (
          <Step key={label}>
            <StepLabel>{label}</StepLabel>
          </Step>
        ))}
      </Stepper>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      {activeStep === 0 && (
        <Box>
          {loadingList ? (
            <LoadingSpinner />
          ) : treinadores.length === 0 ? (
            <Box sx={{ textAlign: "center", py: 4 }}>
              <Button variant="contained" color="primary" onClick={fetchTreinadores}>
                Carregar treinadores
              </Button>
            </Box>
          ) : (
            <Stack spacing={1.5}>
              {treinadores.map((treinador) => (
                <Card
                  key={treinador.treinadorId}
                  variant="outlined"
                  sx={{ borderColor: selectedTreinador?.treinadorId === treinador.treinadorId ? "primary.main" : "divider" }}
                >
                  <CardActionArea onClick={() => handleSelectTreinador(treinador)}>
                    <CardContent sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                      <Radio checked={selectedTreinador?.treinadorId === treinador.treinadorId} size="small" readOnly />
                      <Box>
                        <Typography variant="body1" sx={{ fontWeight: 600 }}>{treinador.nome}</Typography>
                      </Box>
                    </CardContent>
                  </CardActionArea>
                </Card>
              ))}
            </Stack>
          )}
        </Box>
      )}

      {activeStep === 1 && (
        <Box>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Treinador: <strong>{selectedTreinador?.nome}</strong> |{" "}
            <Box component="span" sx={{ cursor: "pointer", textDecoration: "underline" }} onClick={() => goToStep(0)}>
              alterar
            </Box>
          </Typography>
          {loadingList ? (
            <LoadingSpinner />
          ) : pacotes.length === 0 ? (
            <Typography variant="body2" color="text.secondary">
              Nenhum pacote disponivel para este treinador.
            </Typography>
          ) : (
            <Stack spacing={1.5}>
              {pacotes.map((pacote) => (
                <Card
                  key={pacote.pacoteId}
                  variant="outlined"
                  sx={{ borderColor: selectedPacote?.pacoteId === pacote.pacoteId ? "primary.main" : "divider" }}
                >
                  <CardActionArea onClick={() => handleSelectPacote(pacote)}>
                    <CardContent sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                      <Radio checked={selectedPacote?.pacoteId === pacote.pacoteId} size="small" readOnly />
                      <Box>
                        <Typography variant="body1" sx={{ fontWeight: 600 }}>{pacote.nome}</Typography>
                        <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                          Ate {pacote.maxFichas} fichas
                        </Typography>
                        <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                          R$ {Number(pacote.preco).toFixed(2)}
                        </Typography>
                      </Box>
                    </CardContent>
                  </CardActionArea>
                </Card>
              ))}
            </Stack>
          )}
        </Box>
      )}

      {activeStep === 2 && (
        <FormProvider {...methods}>
          <Stack component="form" onSubmit={methods.handleSubmit(onSubmit)} spacing={2}>
            <Typography variant="body2" color="text.secondary">
              Treinador: <strong>{selectedTreinador?.nome}</strong> | Pacote: <strong>{selectedPacote?.nome}</strong>
            </Typography>
            <FormTextField name="nome" label="Nome completo" required autoComplete="name" />
            <FormTextField name="email" label="E-mail" type="email" required autoComplete="email" />
            <FormTextField name="telefone" label="Telefone (opcional)" autoComplete="tel" />
            <PasswordField name="password" label="Senha" required autoComplete="new-password" />
            <PasswordField name="confirmPassword" label="Confirmar senha" required autoComplete="new-password" />

            <Button
              type="submit"
              variant="contained"
              color="primary"
              size="large"
              fullWidth
              disabled={loading}
              startIcon={loading ? <CircularProgress size={18} color="inherit" /> : undefined}
            >
              Criar conta
            </Button>
            <Button variant="text" size="small" onClick={() => goToStep(1)}>
              Voltar
            </Button>
          </Stack>
        </FormProvider>
      )}

      <Box sx={{ mt: 3, textAlign: "center" }}>
        <Typography variant="body2" color="text.secondary">
          Ja tem conta?{" "}
          <Link href="/login" style={{ color: "inherit", fontWeight: 600 }}>
            Entrar
          </Link>
        </Typography>
      </Box>
    </Box>
  );
}
