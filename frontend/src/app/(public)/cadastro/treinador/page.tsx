"use client";
import { useEffect, useState } from "react";
import {
  Box, Typography, Button, CircularProgress, Paper, Stack,
  Radio, RadioGroup, FormControlLabel, FormControl, FormLabel, FormHelperText,
} from "@mui/material";
import { useForm, FormProvider, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircle";
import Link from "next/link";
import FormTextField from "@/components/forms/FormTextField";
import PasswordField from "@/components/forms/PasswordField";
import AlertBanner from "@/components/ui/AlertBanner";
import PagamentoSignup from "@/components/pagamento/PagamentoSignup";
import {
  cadastroTreinadorSchema,
  type CadastroTreinadorFormData,
} from "@/lib/validations/common";
import type {
  IniciarPagamentoPlanoResponse,
  MetodoPagamento,
  PlanoPlataformaResponse,
  ProblemDetails,
  TreinadorResponse,
} from "@/types";

type Finalizado = "analise" | "pix" | "cartao";

function formatBRL(valor: number) {
  return valor === 0 ? "Grátis" : valor.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

export default function CadastroTreinadorPage() {
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [planos, setPlanos] = useState<PlanoPlataformaResponse[] | null>(null);
  const [step, setStep] = useState<1 | 2>(1);
  const [treinadorId, setTreinadorId] = useState("");
  const [pagamento, setPagamento] = useState<IniciarPagamentoPlanoResponse | null>(null);
  const [finalizado, setFinalizado] = useState<Finalizado | null>(null);

  const methods = useForm<CadastroTreinadorFormData>({
    resolver: zodResolver(cadastroTreinadorSchema),
    defaultValues: {
      nome: "", email: "", telefone: "", password: "", confirmPassword: "",
      planoPlataformaId: "", modoPagamentoAluno: "Plataforma",
    },
  });

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const res = await fetch("/api/auth/planos");
        if (!res.ok) throw new Error();
        const data: PlanoPlataformaResponse[] = await res.json();
        if (active) setPlanos(data.filter((p) => p.isAtivo !== false));
      } catch {
        if (active) setError("Não foi possível carregar os planos. Recarregue a página.");
      }
    })();
    return () => { active = false; };
  }, []);

  const onSubmit = async (data: CadastroTreinadorFormData) => {
    setError("");
    setLoading(true);
    try {
      const res = await fetch("/api/auth/register/treinador", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          nome: data.nome,
          email: data.email,
          senha: data.password,
          telefone: data.telefone || undefined,
          planoPlataformaId: data.planoPlataformaId,
          modoPagamentoAluno: data.modoPagamentoAluno,
        }),
      });

      if (!res.ok) {
        if (res.status >= 500) setError("Erro interno. Tente novamente.");
        else {
          const problem: ProblemDetails = await res.json();
          setError(problem.detail ?? problem.title ?? "Erro ao criar conta.");
        }
        return;
      }

      const treinador: TreinadorResponse = await res.json();
      if (treinador.status === "AguardandoPagamento") {
        setTreinadorId(treinador.treinadorId);
        setStep(2);
      } else {
        setFinalizado("analise");
      }
    } catch {
      setError("Não foi possível conectar ao servidor.");
    } finally {
      setLoading(false);
    }
  };

  const iniciarPagamento = async (metodo: MetodoPagamento) => {
    setError("");
    setLoading(true);
    try {
      const res = await fetch(`/api/auth/treinador/${treinadorId}/pagamento`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ metodo }),
      });
      if (!res.ok) {
        const problem: ProblemDetails = await res.json().catch(() => ({}));
        setError(problem.detail ?? problem.title ?? "Erro ao iniciar o pagamento.");
        return;
      }
      const dados: IniciarPagamentoPlanoResponse = await res.json();
      setPagamento(dados);
      if (dados.metodoPagamento === "Pix") setFinalizado("pix");
    } catch {
      setError("Não foi possível conectar ao servidor.");
    } finally {
      setLoading(false);
    }
  };

  if (finalizado) {
    return (
      <Stack spacing={3} sx={{ alignItems: "center", textAlign: "center" }}>
        <Paper elevation={0} variant="outlined" sx={{ p: 4, width: "100%" }}>
          <CheckCircleOutlineIcon sx={{ fontSize: 56, color: "primary.main", mb: 2 }} />
          <Typography variant="h6" sx={{ fontWeight: 700, mb: 1 }}>
            {finalizado === "analise" ? "Solicitação enviada" : "Quase lá"}
          </Typography>
          {finalizado === "pix" && pagamento ? (
            <Box sx={{ mb: 1 }}><PagamentoSignup pagamento={pagamento} onPagoCartao={() => {}} /></Box>
          ) : (
            <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
              {finalizado === "analise"
                ? "Seu cadastro está em análise. Você receberá o acesso assim que for validado pela equipe."
                : "Pagamento recebido! Verifique seu e-mail para confirmar o cadastro."}
            </Typography>
          )}
          <Box sx={{ mt: 3 }}>
            <Link href="/login" style={{ textDecoration: "none" }}>
              <Button variant="contained" color="primary">Ir para o login</Button>
            </Link>
          </Box>
        </Paper>
      </Stack>
    );
  }

  if (step === 2) {
    return (
      <Box>
        <Typography variant="h5" sx={{ fontWeight: 700, mb: 0.5 }}>Pagamento do plano</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
          Escolha como pagar a assinatura da plataforma para concluir o cadastro.
        </Typography>

        <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

        {pagamento ? (
          <PagamentoSignup pagamento={pagamento} onPagoCartao={() => setFinalizado("cartao")} />
        ) : (
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <Button variant="contained" fullWidth disabled={loading} onClick={() => iniciarPagamento("Pix")}>
              {loading ? <CircularProgress size={18} /> : "Pagar com Pix"}
            </Button>
            <Button variant="outlined" fullWidth disabled={loading} onClick={() => iniciarPagamento("Cartao")}>
              Pagar com cartão
            </Button>
          </Stack>
        )}
      </Box>
    );
  }

  return (
    <Box>
      <Typography variant="h5" sx={{ fontWeight: 700, mb: 0.5 }}>Criar conta profissional</Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        Escolha seu plano e como seus alunos vão pagar. O acesso é liberado após validação.
      </Typography>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      <FormProvider {...methods}>
        <Stack component="form" onSubmit={methods.handleSubmit(onSubmit)} spacing={2}>
          <FormTextField name="nome" label="Nome completo" required autoComplete="name" />
          <FormTextField name="email" label="E-mail" type="email" required autoComplete="email" />
          <FormTextField name="telefone" label="WhatsApp (opcional)" type="tel" autoComplete="tel" placeholder="11999999999" />
          <PasswordField name="password" label="Senha" required autoComplete="new-password" />
          <PasswordField name="confirmPassword" label="Confirmar senha" required autoComplete="new-password" />

          <Controller
            name="planoPlataformaId"
            control={methods.control}
            render={({ field, fieldState }) => (
              <FormControl error={!!fieldState.error} required>
                <FormLabel>Plano da plataforma</FormLabel>
                {planos === null ? (
                  <CircularProgress size={22} sx={{ mt: 1 }} />
                ) : (
                  <RadioGroup {...field}>
                    {planos.map((p) => (
                      <FormControlLabel
                        key={p.planoId}
                        value={p.planoId}
                        control={<Radio />}
                        label={`${p.nome} — ${formatBRL(p.preco)} · até ${p.maxAlunos} alunos`}
                      />
                    ))}
                  </RadioGroup>
                )}
                {fieldState.error && <FormHelperText>{fieldState.error.message}</FormHelperText>}
              </FormControl>
            )}
          />

          <Controller
            name="modoPagamentoAluno"
            control={methods.control}
            render={({ field }) => (
              <FormControl>
                <FormLabel>Como seus alunos vão pagar você?</FormLabel>
                <RadioGroup {...field}>
                  <FormControlLabel value="Plataforma" control={<Radio />} label="Pela plataforma (cobrança automática via Stripe)" />
                  <FormControlLabel value="Externo" control={<Radio />} label="Por fora (você combina e recebe direto; sem cobrança na plataforma)" />
                </RadioGroup>
              </FormControl>
            )}
          />

          <Button type="submit" variant="contained" color="primary" size="large" fullWidth disabled={loading}
            startIcon={loading ? <CircularProgress size={18} color="inherit" /> : undefined}>
            Continuar
          </Button>
        </Stack>
      </FormProvider>

      <Box sx={{ mt: 3, textAlign: "center" }}>
        <Typography variant="body2" color="text.secondary">
          Já tem conta?{" "}
          <Link href="/login" style={{ color: "inherit", fontWeight: 600 }}>Entrar</Link>
        </Typography>
      </Box>
    </Box>
  );
}
