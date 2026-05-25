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
  Divider,
} from "@mui/material";
import { useForm, FormProvider } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircle";
import Link from "next/link";
import FormTextField from "@/components/forms/FormTextField";
import FormSelect from "@/components/forms/FormSelect";
import PasswordField from "@/components/forms/PasswordField";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import { cadastroAlunoSchema, type CadastroAlunoFormData } from "@/lib/validations/common";
import type { ProblemDetails, TreinadorResponse, PacoteResponse } from "@/types";
import { DIAS_OPTIONS, TEMPO_OPTIONS, FINALIDADE_OPTIONS, NIVEL_OPTIONS } from "@/lib/constants/enrollmentOptions";

const STEPS = ["Escolher treinador", "Escolher pacote", "Seus dados", "Informações adicionais"];

const STEP2_FIELDS: (keyof CadastroAlunoFormData)[] = [
  "nome",
  "email",
  "telefone",
  "password",
  "confirmPassword",
];

export default function CadastroAlunoPage() {
  const [activeStep, setActiveStep] = useState(0);
  const [treinadores, setTreinadores] = useState<TreinadorResponse[]>([]);
  const [pacotes, setPacotes] = useState<PacoteResponse[]>([]);
  const [selectedTreinador, setSelectedTreinador] = useState<TreinadorResponse | null>(null);
  const [selectedPacote, setSelectedPacote] = useState<PacoteResponse | null>(null);
  const [loadingList, setLoadingList] = useState(false);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(false);

  const methods = useForm<CadastroAlunoFormData>({
    resolver: zodResolver(cadastroAlunoSchema),
    defaultValues: {
      nome: "",
      email: "",
      telefone: "",
      password: "",
      confirmPassword: "",
      diasDisponiveis: "",
      tempoDisponivelMinutos: "",
      finalidade: "",
      nivelCondicionamento: "",
      focoTreino: "",
      limitacoesFisicas: "",
      doencas: "",
      observacoesAdicionais: "",
    },
  });

  const fetchTreinadores = async () => {
    setLoadingList(true);
    setError("");
    try {
      const res = await fetch("/api/auth/treinadores");
      if (!res.ok) throw new Error();
      setTreinadores(await res.json());
    } catch {
      setError("Não foi possível carregar os treinadores.");
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
      setError("Não foi possível carregar os pacotes.");
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

  const handleSelectPacote = (pacote: PacoteResponse) => {
    setSelectedPacote(pacote);
    setActiveStep(2);
  };

  const handleNextFromStep2 = async () => {
    const valid = await methods.trigger(STEP2_FIELDS);
    if (valid) setActiveStep(3);
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
          diasDisponiveis: data.diasDisponiveis ? parseInt(data.diasDisponiveis) : null,
          tempoDisponivelMinutos: data.tempoDisponivelMinutos
            ? parseInt(data.tempoDisponivelMinutos)
            : null,
          finalidade: data.finalidade || null,
          focoTreino: data.focoTreino || null,
          nivelCondicionamento: data.nivelCondicionamento || null,
          limitacoesFisicas: data.limitacoesFisicas || null,
          doencas: data.doencas || null,
          observacoesAdicionais: data.observacoesAdicionais || null,
        }),
      });

      if (!res.ok) {
        if (res.status >= 500) {
          setError("Erro interno. Tente novamente.");
        } else {
          const problem: ProblemDetails = await res.json();
          setError(problem.detail ?? problem.title ?? "Erro ao criar conta.");
        }
        return;
      }

      setSuccess(true);
    } catch {
      setError("Não foi possível conectar ao servidor.");
    } finally {
      setLoading(false);
    }
  };

  if (success) {
    return (
      <Paper elevation={0} variant="outlined" sx={{ p: 4, textAlign: "center" }}>
        <CheckCircleOutlineIcon sx={{ fontSize: 56, color: "primary.main", mb: 2 }} />
        <Typography variant="h6" sx={{ fontWeight: 700, mb: 1 }}>
          Solicitação de vínculo enviada
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
          Seu vínculo com <strong>{selectedTreinador?.nome}</strong> está aguardando aprovação.
          Após a confirmação, você terá acesso às fichas de treino.
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
        Selecione seu treinador e o plano de atendimento para iniciar.
      </Typography>

      <Stepper activeStep={activeStep} alternativeLabel sx={{ mb: 4 }}>
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
              Este treinador não possui planos de atendimento disponíveis no momento.
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
                          {pacote.descricao ?? ""}
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
          <Stack spacing={2}>
            <Typography variant="body2" color="text.secondary">
              Treinador: <strong>{selectedTreinador?.nome}</strong> | Pacote: <strong>{selectedPacote?.nome}</strong>
            </Typography>
            <FormTextField name="nome" label="Nome completo" required autoComplete="name" />
            <FormTextField name="email" label="E-mail" type="email" required autoComplete="email" />
            <FormTextField name="telefone" label="Celular" required autoComplete="tel" helperText="Somente dígitos, ex: 11987654321" />
            <PasswordField name="password" label="Senha" required autoComplete="new-password" />
            <PasswordField name="confirmPassword" label="Confirmar senha" required autoComplete="new-password" />

            <Button
              variant="contained"
              color="primary"
              size="large"
              fullWidth
              onClick={handleNextFromStep2}
            >
              Próximo
            </Button>
            <Button variant="text" size="small" onClick={() => goToStep(1)}>
              Voltar
            </Button>
          </Stack>
        </FormProvider>
      )}

      {activeStep === 3 && (
        <FormProvider {...methods}>
          <Stack component="form" onSubmit={methods.handleSubmit(onSubmit)} spacing={2}>
            <Typography variant="body2" color="text.secondary">
              Treinador: <strong>{selectedTreinador?.nome}</strong> | Pacote: <strong>{selectedPacote?.nome}</strong>
            </Typography>

            <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
              Disponibilidade
            </Typography>
            <FormSelect
              name="diasDisponiveis"
              label="Dias disponíveis por semana"
              options={DIAS_OPTIONS}
              required
            />
            <FormSelect
              name="tempoDisponivelMinutos"
              label="Tempo disponível por dia"
              options={TEMPO_OPTIONS}
              required
            />

            <Divider />

            <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
              Objetivos
            </Typography>
            <FormSelect
              name="finalidade"
              label="Finalidade do treino"
              options={FINALIDADE_OPTIONS}
              required
            />
            <FormTextField
              name="focoTreino"
              label="Foco de treino (opcional)"
              placeholder="Ex.: membros inferiores, postura, core..."
              size="small"
            />
            <FormSelect
              name="nivelCondicionamento"
              label="Nível de condicionamento atual"
              options={NIVEL_OPTIONS}
              required
            />

            <Divider />

            <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
              Saúde
            </Typography>
            <FormTextField
              name="limitacoesFisicas"
              label="Limitações físicas (opcional)"
              placeholder="Ex.: dor no joelho, hérnia de disco, ombro operado..."
              multiline
              rows={2}
              size="small"
            />
            <FormTextField
              name="doencas"
              label="Doenças ou condições de saúde (opcional)"
              placeholder="Ex.: hipertensão, diabetes, hipotireoidismo..."
              multiline
              rows={2}
              size="small"
            />

            <Divider />

            <FormTextField
              name="observacoesAdicionais"
              label="Observações adicionais (opcional)"
              placeholder="Qualquer informação relevante para o seu treinador..."
              multiline
              rows={3}
              size="small"
            />

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
            <Button variant="text" size="small" onClick={() => setActiveStep(2)}>
              Voltar
            </Button>
          </Stack>
        </FormProvider>
      )}

      <Box sx={{ mt: 3, textAlign: "center" }}>
        <Typography variant="body2" color="text.secondary">
          Já tem conta?{" "}
          <Link href="/login" style={{ color: "inherit", fontWeight: 600 }}>
            Entrar
          </Link>
        </Typography>
      </Box>
    </Box>
  );
}
