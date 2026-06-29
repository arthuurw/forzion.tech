"use client";
import { useEffect, useState } from "react";
import {
  Box, Card, CardContent, Button, CircularProgress, Grid,
} from "@mui/material";
import { useForm, FormProvider, Controller, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { TextField } from "@mui/material";
import FormTextField from "@/components/forms/FormTextField";
import FormSelect from "@/components/forms/FormSelect";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import PageHeader from "@/components/ui/PageHeader";
import { nfseApi } from "@/lib/api/nfse";
import { extractApiError } from "@/lib/api/extractApiError";
import {
  dadosFiscaisSchema, type DadosFiscaisFormData,
  mascararDocumento, mascararCep, soDigitos,
} from "@/lib/validations/dadosFiscais";

const TIPO_OPTIONS = [
  { value: "Cpf", label: "CPF" },
  { value: "Cnpj", label: "CNPJ" },
];

const DEFAULTS: DadosFiscaisFormData = {
  tipoDocumento: "Cpf",
  documento: "",
  razaoSocial: "",
  inscricaoMunicipal: "",
  logradouro: "",
  numero: "",
  complemento: "",
  bairro: "",
  codigoMunicipioIbge: "",
  uf: "",
  cep: "",
};

export default function DadosFiscaisTreinadorPage() {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const methods = useForm<DadosFiscaisFormData>({
    resolver: zodResolver(dadosFiscaisSchema),
    defaultValues: DEFAULTS,
  });
  const tipoDocumento = useWatch({ control: methods.control, name: "tipoDocumento" });

  useEffect(() => {
    nfseApi
      .getDadosFiscais()
      .then((res) => {
        const d = res.data;
        if (!d) return;
        methods.reset({
          tipoDocumento: d.tipoDocumento,
          documento: mascararDocumento(d.tipoDocumento, d.documento),
          razaoSocial: d.razaoSocial,
          inscricaoMunicipal: d.inscricaoMunicipal ?? "",
          logradouro: d.endereco.logradouro,
          numero: d.endereco.numero,
          complemento: d.endereco.complemento ?? "",
          bairro: d.endereco.bairro,
          codigoMunicipioIbge: d.endereco.codigoMunicipioIbge,
          uf: d.endereco.uf,
          cep: mascararCep(d.endereco.cep),
        });
      })
      .catch(() => setError("Não foi possível carregar seus dados fiscais."))
      .finally(() => setLoading(false));
  }, [methods]);

  const onSubmit = async (data: DadosFiscaisFormData) => {
    setError("");
    setSuccess("");
    setSaving(true);
    try {
      await nfseApi.salvarDadosFiscais({
        tipoDocumento: data.tipoDocumento,
        documento: soDigitos(data.documento),
        razaoSocial: data.razaoSocial.trim(),
        logradouro: data.logradouro.trim(),
        numero: data.numero.trim(),
        bairro: data.bairro.trim(),
        codigoMunicipioIbge: data.codigoMunicipioIbge,
        uf: data.uf.trim().toUpperCase(),
        cep: soDigitos(data.cep),
        complemento: data.complemento?.trim() || null,
        inscricaoMunicipal: data.inscricaoMunicipal?.trim() || null,
      });
      setSuccess("Dados fiscais salvos.");
    } catch (err) {
      setError(extractApiError(err, "Não foi possível salvar os dados fiscais."));
    } finally {
      setSaving(false);
    }
  };

  if (loading) return <LoadingSpinner />;

  return (
    <Box sx={{ maxWidth: { xs: "100%", md: 640 } }}>
      <PageHeader
        title="Dados fiscais"
        subtitle="Usados como tomador na emissão das suas notas fiscais de serviço (NFS-e)."
      />

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!success} severity="success" message={success} onClose={() => setSuccess("")} />

      <Card sx={{ border: "1px solid", borderColor: "divider" }}>
        <CardContent sx={{ p: 3, "&:last-child": { pb: 3 } }}>
          <FormProvider {...methods}>
            <Box component="form" aria-label="Dados fiscais" onSubmit={methods.handleSubmit(onSubmit)}>
              <Grid container spacing={2}>
                <Grid size={{ xs: 12, sm: 4 }}>
                  <FormSelect name="tipoDocumento" label="Tipo de documento" options={TIPO_OPTIONS} required />
                </Grid>
                <Grid size={{ xs: 12, sm: 8 }}>
                  <Controller
                    name="documento"
                    control={methods.control}
                    render={({ field, fieldState }) => (
                      <TextField
                        {...field}
                        onChange={(e) => field.onChange(mascararDocumento(tipoDocumento, e.target.value))}
                        label={tipoDocumento === "Cpf" ? "CPF" : "CNPJ"}
                        size="small"
                        fullWidth
                        required
                        error={!!fieldState.error}
                        helperText={fieldState.error?.message}
                      />
                    )}
                  />
                </Grid>
                <Grid size={{ xs: 12 }}>
                  <FormTextField name="razaoSocial" label="Nome / Razão social" size="small" fullWidth required />
                </Grid>
                <Grid size={{ xs: 12, sm: 6 }}>
                  <FormTextField name="inscricaoMunicipal" label="Inscrição municipal (opcional)" size="small" fullWidth />
                </Grid>

                <Grid size={{ xs: 12, sm: 8 }}>
                  <FormTextField name="logradouro" label="Logradouro" size="small" fullWidth required />
                </Grid>
                <Grid size={{ xs: 12, sm: 4 }}>
                  <FormTextField name="numero" label="Número" size="small" fullWidth required />
                </Grid>
                <Grid size={{ xs: 12, sm: 6 }}>
                  <FormTextField name="complemento" label="Complemento (opcional)" size="small" fullWidth />
                </Grid>
                <Grid size={{ xs: 12, sm: 6 }}>
                  <FormTextField name="bairro" label="Bairro" size="small" fullWidth required />
                </Grid>
                <Grid size={{ xs: 12, sm: 4 }}>
                  <Controller
                    name="cep"
                    control={methods.control}
                    render={({ field, fieldState }) => (
                      <TextField
                        {...field}
                        onChange={(e) => field.onChange(mascararCep(e.target.value))}
                        label="CEP"
                        size="small"
                        fullWidth
                        required
                        error={!!fieldState.error}
                        helperText={fieldState.error?.message}
                      />
                    )}
                  />
                </Grid>
                <Grid size={{ xs: 12, sm: 5 }}>
                  <FormTextField
                    name="codigoMunicipioIbge"
                    label="Código IBGE do município"
                    size="small"
                    fullWidth
                    required
                    slotProps={{ htmlInput: { maxLength: 7, inputMode: "numeric" } }}
                  />
                </Grid>
                <Grid size={{ xs: 12, sm: 3 }}>
                  <FormTextField
                    name="uf"
                    label="UF"
                    size="small"
                    fullWidth
                    required
                    slotProps={{ htmlInput: { maxLength: 2, style: { textTransform: "uppercase" } } }}
                  />
                </Grid>
              </Grid>

              <Box sx={{ display: "flex", justifyContent: "flex-end", mt: 3 }}>
                <Button
                  type="submit"
                  variant="contained"
                  color="primary"
                  disabled={saving}
                  startIcon={saving ? <CircularProgress size={18} color="inherit" /> : undefined}
                >
                  Salvar dados fiscais
                </Button>
              </Box>
            </Box>
          </FormProvider>
        </CardContent>
      </Card>
    </Box>
  );
}
