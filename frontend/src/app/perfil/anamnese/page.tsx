"use client";
import { useEffect, useState } from "react";
import {
  Box,
  Typography,
  Card,
  CardContent,
  Stack,
  Button,
  Divider,
  FormControlLabel,
  Checkbox,
  CircularProgress,
} from "@mui/material";
import { useForm, FormProvider } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import Link from "next/link";
import FormTextField from "@/components/forms/FormTextField";
import FormSelect from "@/components/forms/FormSelect";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import { anamneseSchema, type AnamneseFormData } from "@/lib/validations/common";
import { DIAS_OPTIONS, TEMPO_OPTIONS, FINALIDADE_OPTIONS, NIVEL_OPTIONS } from "@/lib/constants/enrollmentOptions";
import { alunoApi } from "@/lib/api/aluno";
import { extractApiError } from "@/lib/api/extractApiError";
import { useAuth } from "@/lib/auth/context";

export default function AnamnesePage() {
  const { user } = useAuth();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [consentimentoSaude, setConsentimentoSaude] = useState(false);

  const methods = useForm<AnamneseFormData>({
    resolver: zodResolver(anamneseSchema),
    defaultValues: {
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

  useEffect(() => {
    if (!user) return;
    const load = async () => {
      try {
        const res = await alunoApi.getMeuPerfilAluno(user.perfilId);
        const a = res.data;
        methods.reset({
          diasDisponiveis: a.diasDisponiveis != null ? String(a.diasDisponiveis) : "",
          tempoDisponivelMinutos: a.tempoDisponivelMinutos != null ? String(a.tempoDisponivelMinutos) : "",
          finalidade: a.finalidade ?? "",
          nivelCondicionamento: a.nivelCondicionamento ?? "",
          focoTreino: a.focoTreino ?? "",
          limitacoesFisicas: a.limitacoesFisicas ?? "",
          doencas: a.doencas ?? "",
          observacoesAdicionais: a.observacoesAdicionais ?? "",
        });
      } catch (err) {
        setError(extractApiError(err, "Erro ao carregar anamnese."));
      } finally {
        setLoading(false);
      }
    };
    load();
  }, [user, methods]);

  const values = methods.watch();
  const coletaDadosSaude =
    !!values.finalidade ||
    !!values.nivelCondicionamento ||
    !!values.focoTreino?.trim() ||
    !!values.limitacoesFisicas?.trim() ||
    !!values.doencas?.trim() ||
    !!values.observacoesAdicionais?.trim();
  const consentimentoPendente = coletaDadosSaude && !consentimentoSaude;

  const onSubmit = async (data: AnamneseFormData) => {
    if (consentimentoPendente) return;
    setSaving(true);
    setError("");
    setSuccess("");
    try {
      await alunoApi.atualizarAnamnese({
        diasDisponiveis: data.diasDisponiveis ? parseInt(data.diasDisponiveis) : null,
        tempoDisponivelMinutos: data.tempoDisponivelMinutos ? parseInt(data.tempoDisponivelMinutos) : null,
        finalidade: data.finalidade || null,
        focoTreino: data.focoTreino || null,
        nivelCondicionamento: data.nivelCondicionamento || null,
        limitacoesFisicas: data.limitacoesFisicas || null,
        doencas: data.doencas || null,
        observacoesAdicionais: data.observacoesAdicionais || null,
        consentimentoDadosSaude: coletaDadosSaude,
        consentimentoDadosSaudeEm: coletaDadosSaude ? new Date().toISOString() : null,
      });
      setSuccess("Anamnese atualizada com sucesso.");
    } catch (err) {
      setError(extractApiError(err, "Erro ao atualizar anamnese."));
    } finally {
      setSaving(false);
    }
  };

  if (loading) return <LoadingSpinner />;

  return (
    <Box sx={{ maxWidth: { xs: "100%", md: 580 } }}>
      <Box sx={{ mb: 4 }}>
        <Typography variant="h5" sx={{ fontWeight: 700 }}>Minha anamnese</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
          Atualize as informações que orientam o seu treino (LGPD art. 18, III).
        </Typography>
      </Box>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!success} severity="success" message={success} onClose={() => setSuccess("")} />

      <Card sx={{ border: "1px solid", borderColor: "divider" }}>
        <CardContent sx={{ p: 3, "&:last-child": { pb: 3 } }}>
          <FormProvider {...methods}>
            <Stack component="form" onSubmit={methods.handleSubmit(onSubmit)} spacing={2}>
              <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>Disponibilidade</Typography>
              <FormSelect name="diasDisponiveis" label="Dias disponíveis por semana" options={DIAS_OPTIONS} required />
              <FormSelect name="tempoDisponivelMinutos" label="Tempo disponível por dia" options={TEMPO_OPTIONS} required />

              <Divider />

              <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>Objetivos</Typography>
              <FormSelect name="finalidade" label="Finalidade do treino" options={FINALIDADE_OPTIONS} required />
              <FormTextField name="focoTreino" label="Foco de treino (opcional)" placeholder="Ex.: membros inferiores, postura, core..." size="small" />
              <FormSelect name="nivelCondicionamento" label="Nível de condicionamento atual" options={NIVEL_OPTIONS} required />

              <Divider />

              <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>Saúde</Typography>
              <FormTextField name="limitacoesFisicas" label="Limitações físicas (opcional)" placeholder="Ex.: dor no joelho, hérnia de disco..." multiline rows={2} size="small" />
              <FormTextField name="doencas" label="Doenças ou condições de saúde (opcional)" placeholder="Ex.: hipertensão, diabetes..." multiline rows={2} size="small" />
              <FormTextField name="observacoesAdicionais" label="Observações adicionais (opcional)" multiline rows={3} size="small" />

              <Divider />

              <FormControlLabel
                control={
                  <Checkbox
                    checked={consentimentoSaude}
                    onChange={(e) => setConsentimentoSaude(e.target.checked)}
                    size="small"
                  />
                }
                label="Concordo com o tratamento dos meus dados de saúde para fins de orientação de treino."
                slotProps={{ typography: { variant: "body2", color: "text.secondary" } }}
              />

              <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end" }}>
                <Button component={Link} href="/perfil" variant="text" size="small">Voltar</Button>
                <Button
                  type="submit"
                  variant="contained"
                  disabled={saving || consentimentoPendente}
                  startIcon={saving ? <CircularProgress size={18} color="inherit" /> : undefined}
                >
                  {saving ? "Salvando..." : "Salvar anamnese"}
                </Button>
              </Stack>
            </Stack>
          </FormProvider>
        </CardContent>
      </Card>
    </Box>
  );
}
