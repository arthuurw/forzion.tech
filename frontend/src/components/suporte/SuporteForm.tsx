"use client";
import { useEffect, useState } from "react";
import { Box, Typography, Card, CardContent, TextField, Button, CircularProgress } from "@mui/material";
import { useForm, FormProvider } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import FormTextField from "@/components/forms/FormTextField";
import FormSelect from "@/components/forms/FormSelect";
import AlertBanner from "@/components/ui/AlertBanner";
import PageHeader from "@/components/ui/PageHeader";
import { contaApi, type PerfilResponse } from "@/lib/api/conta";
import { apiClient } from "@/lib/api/client";
import { extractApiError } from "@/lib/api/extractApiError";
import { suporteSchema, type SuporteFormData, CATEGORIAS_SUPORTE, CATEGORIA_LABEL } from "@/lib/validations/suporte";

const CATEGORIA_OPTIONS = CATEGORIAS_SUPORTE.map((c) => ({ value: c, label: CATEGORIA_LABEL[c] }));

export default function SuporteForm() {
  const [perfil, setPerfil] = useState<PerfilResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [sending, setSending] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState("");

  const methods = useForm<SuporteFormData>({
    resolver: zodResolver(suporteSchema),
    defaultValues: { categoria: "Duvida", assunto: "", descricao: "" },
  });

  useEffect(() => {
    contaApi
      .getPerfil()
      .then((res) => setPerfil(res.data))
      .catch(() => setError("Não foi possível carregar seus dados de cadastro."))
      .finally(() => setLoading(false));
  }, []);

  // Identidade (nome/e-mail) vem do token no backend — o payload carrega só assunto/descrição/categoria.
  const onSubmit = async (data: SuporteFormData) => {
    setError("");
    setSending(true);
    try {
      await apiClient.post("/suporte/mensagens", {
        categoria: data.categoria,
        assunto: data.assunto,
        descricao: data.descricao,
      });
      setSent(true);
    } catch (err) {
      setError(extractApiError(err, "Não foi possível enviar sua mensagem. Tente novamente."));
    } finally {
      setSending(false);
    }
  };

  if (loading) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", py: 6 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (sent) {
    return (
      <Box sx={{ maxWidth: { xs: "100%", md: 580 } }}>
        <Card sx={{ border: "1px solid", borderColor: "divider" }}>
          <CardContent sx={{ p: 3, "&:last-child": { pb: 3 } }}>
            <Typography variant="h6" sx={{ mb: 0.5 }}>
              Mensagem enviada
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
              Recebemos sua mensagem e responderemos no e-mail do seu cadastro em breve.
            </Typography>
            <Button variant="contained" onClick={() => { methods.reset(); setSent(false); }}>
              Enviar outra mensagem
            </Button>
          </CardContent>
        </Card>
      </Box>
    );
  }

  return (
    <Box sx={{ maxWidth: { xs: "100%", md: 580 } }}>
      <PageHeader
        title="Falar com o suporte"
        subtitle="Tem uma dúvida ou sugestão? Envie sua mensagem e responderemos por e-mail."
      />

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      <Card sx={{ border: "1px solid", borderColor: "divider" }}>
        <CardContent sx={{ p: 3, "&:last-child": { pb: 3 } }}>
          <FormProvider {...methods}>
            <Box
              component="form"
              onSubmit={methods.handleSubmit(onSubmit)}
              sx={{ display: "flex", flexDirection: "column", gap: 2 }}
            >
              <TextField label="Nome" value={perfil?.nome ?? ""} size="small" fullWidth disabled />
              <TextField label="E-mail" value={perfil?.email ?? ""} size="small" fullWidth disabled />
              <FormSelect name="categoria" label="Categoria" options={CATEGORIA_OPTIONS} required />
              <FormTextField name="assunto" label="Assunto" size="small" required slotProps={{ htmlInput: { maxLength: 120 } }} />
              <FormTextField
                name="descricao"
                label="Descrição"
                size="small"
                required
                multiline
                minRows={4}
                slotProps={{ htmlInput: { maxLength: 2000 } }}
              />
              <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
                <Button
                  type="submit"
                  variant="contained"
                  color="primary"
                  disabled={sending}
                  startIcon={sending ? <CircularProgress size={18} color="inherit" /> : undefined}
                >
                  Enviar mensagem
                </Button>
              </Box>
            </Box>
          </FormProvider>
        </CardContent>
      </Card>
    </Box>
  );
}
