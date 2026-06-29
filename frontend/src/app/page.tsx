import { Box, Container, Typography, Button, Grid, Card, CardContent, Chip } from "@mui/material";
import type { Metadata } from "next";
import Link from "next/link";
import Logo from "@/components/ui/Logo";
import HowItWorks from "./_landing/HowItWorks";
import SocialProof from "./_landing/SocialProof";
import Diferenciais from "./_landing/Diferenciais";
import Faq from "./_landing/Faq";
import SectionEyebrow from "./_landing/SectionEyebrow";
import type { PlanoPlataformaResponse } from "@/types";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import CheckIcon from "@mui/icons-material/Check";
import * as Sentry from "@sentry/nextjs";
import { CDC_CANCEL_NOTICE } from "@/lib/constants/billing";
import { formatarBRL } from "@/lib/utils/formatting";

const SITE_URL = process.env.NEXT_PUBLIC_SITE_URL ?? "https://forzion.tech";

export const metadata: Metadata = {
  alternates: { canonical: "/" },
};

export const revalidate = 300;

const organizationJsonLd = {
  "@context": "https://schema.org",
  "@type": "Organization",
  name: "forzion.tech",
  url: SITE_URL,
  description: "Plataforma de gestão de treinos para personal trainers",
};

async function getPlanos(): Promise<PlanoPlataformaResponse[]> {
  try {
    const base = process.env.API_BASE_URL ?? "https://localhost:7220";
    const res = await fetch(`${base}/auth/planos`, { next: { revalidate: 300 } });
    if (!res.ok) return [];
    return res.json();
  } catch (err) {
    Sentry.captureException(err);
    return [];
  }
}

export default async function LandingPage() {
  const planos = await getPlanos();

  return (
    <Box sx={{ display: "flex", flexDirection: "column", minHeight: "100vh", bgcolor: "background.default" }}>
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(organizationJsonLd) }}
      />
      {/* Header */}
      <Box
        component="header"
        sx={{
          py: 2,
          px: { xs: 3, md: 5 },
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          bgcolor: "secondary.main",
          position: "sticky",
          top: 0,
          zIndex: 100,
          borderBottom: "2px solid",
          borderColor: "primary.main",
        }}
      >
        <Logo size="md" sx={{ "& span": { color: "rgba(255,255,255,0.9)" }, "& span:first-of-type": { color: "primary.main" } }} />
        <Box sx={{ display: "flex", gap: 1.5 }}>
          <Link href="/login" style={{ textDecoration: "none" }}>
            <Button
              variant="outlined"
              size="small"
              sx={{ borderColor: "rgba(255,255,255,0.3)", color: "white", "&:hover": { borderColor: "white", bgcolor: "rgba(255,255,255,0.06)" } }}
            >
              Entrar
            </Button>
          </Link>
          <Link href="/cadastro/treinador" style={{ textDecoration: "none" }}>
            <Button variant="contained" color="primary" size="small">
              Começar grátis
            </Button>
          </Link>
        </Box>
      </Box>

      <Box component="main" sx={{ flex: 1 }}>
        {/* Hero */}
        <Box
          sx={{
            bgcolor: "secondary.main",
            color: "white",
            py: { xs: 10, md: 16 },
            textAlign: "center",
            position: "relative",
            overflow: "hidden",
            "&::before": {
              content: '""',
              position: "absolute",
              top: "50%",
              left: "50%",
              transform: "translate(-50%, -50%)",
              width: 700,
              height: 700,
              borderRadius: "50%",
              bgcolor: "primary.main",
              opacity: 0.04,
            },
          }}
        >
          <Container maxWidth="md" sx={{ position: "relative" }}>
            <Box
              sx={{
                display: "inline-flex",
                alignItems: "center",
                gap: 1,
                px: 2,
                py: 0.75,
                borderRadius: 10,
                border: "1px solid",
                borderColor: "rgba(245,196,0,0.3)",
                bgcolor: "rgba(245,196,0,0.08)",
                mb: 3,
              }}
            >
              <Box sx={{ width: 7, height: 7, borderRadius: "50%", bgcolor: "primary.main" }} />
              <Typography variant="caption" sx={{ color: "primary.main", fontWeight: 600, letterSpacing: "0.05em" }}>
                SOFTWARE DE GESTÃO PARA PERSONAL TRAINERS
              </Typography>
            </Box>

            <Typography
              variant="h2"
              component="h1"
              sx={{ mb: 2.5, fontWeight: 800, fontSize: { xs: "2.25rem", md: "3.25rem" }, lineHeight: 1.15 }}
            >
              Profissionalize a gestão{" "}
              <Box component="span" sx={{ color: "primary.main" }}>
                do seu studio
              </Box>
            </Typography>
            <Typography variant="h6" sx={{ opacity: 0.6, mb: 5, fontWeight: 400, maxWidth: 560, mx: "auto", lineHeight: 1.7 }}>
              Pare de perder tempo com planilha e WhatsApp. Gerencie fichas, alunos e histórico num só lugar — do jeito que um profissional merece.
            </Typography>
            <Box sx={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 2 }}>
              <Link href="/cadastro/treinador" style={{ textDecoration: "none" }}>
                <Button variant="contained" color="primary" size="large" endIcon={<ArrowForwardIcon />}>
                  Criar conta grátis
                </Button>
              </Link>
              <Link href="/cadastro/aluno" style={{ textDecoration: "none" }}>
                <Typography
                  variant="caption"
                  sx={{
                    color: "rgba(255,255,255,0.45)",
                    "&:hover": { color: "rgba(255,255,255,0.7)" },
                    "&:focus-visible": { outline: "2px solid", outlineColor: "primary.main", outlineOffset: "2px" },
                    textDecoration: "underline",
                    cursor: "pointer",
                    transition: "color 0.2s",
                    // alvo de toque ≥44px (era ~16px de altura de linha — R22).
                    display: "inline-flex",
                    alignItems: "center",
                    minHeight: 44,
                    px: 1,
                  }}
                >
                  Já foi convidado por um treinador? Acesse aqui como aluno
                </Typography>
              </Link>
            </Box>
          </Container>
        </Box>

        {/* Social proof — renders null when empty (beta: fabricating testimonials harms credibility) */}
        <SocialProof testimonials={[]} />

        {/* Como funciona */}
        <HowItWorks />

        <Diferenciais />

        {/* Planos */}
        {planos.length > 0 && (
          <Box sx={{ bgcolor: "secondary.main", py: { xs: 8, md: 12 } }}>
            <Container maxWidth="md">
              <Box sx={{ textAlign: "center", mb: 6 }}>
                <SectionEyebrow label="PLANOS" variant="dark" />
                <Typography variant="h4" sx={{ mt: 2, mb: 1, color: "white" }}>
                  Planos para cada porte de operação
                </Typography>
                <Typography variant="body1" sx={{ color: "rgba(255,255,255,0.72)" }}>
                  Escale conforme sua carteira de alunos cresce
                </Typography>
              </Box>
              <Grid container spacing={3} sx={{ justifyContent: "center" }}>
                {planos.map((plano, i) => {
                  const isInativo = plano.isAtivo === false;
                  const card = (
                    <Card
                      sx={{
                        textAlign: "center",
                        p: 1,
                        height: "100%",
                        width: "100%",
                        display: "flex",
                        flexDirection: "column",
                        bgcolor: i === 1 ? "primary.main" : "rgba(255,255,255,0.06)",
                        border: "1px solid",
                        borderColor: i === 1 ? "primary.main" : "rgba(255,255,255,0.1)",
                        boxShadow: i === 1 ? "0 8px 32px rgba(245,196,0,0.25)" : "none",
                        ...(isInativo
                          ? { pointerEvents: "none", cursor: "default" }
                          : {
                              cursor: "pointer",
                              transition: "transform 0.18s ease, box-shadow 0.18s ease, border-color 0.18s ease",
                              "&:hover": {
                                transform: "scale(1.03)",
                                boxShadow: i === 1 ? "0 14px 44px rgba(245,196,0,0.38)" : "0 4px 24px rgba(255,255,255,0.1)",
                                borderColor: i === 1 ? "primary.main" : "rgba(255,255,255,0.35)",
                              },
                            }),
                      }}
                    >
                      <CardContent sx={{ flex: 1, display: "flex", flexDirection: "column", justifyContent: "center" }}>
                        <Box sx={{ display: "flex", alignItems: "center", justifyContent: "center", gap: 1, mb: 0.5 }}>
                          <Typography variant="h6" sx={{ color: i === 1 ? "secondary.main" : "white" }}>
                            {plano.nome}
                          </Typography>
                          {isInativo && (
                            <Chip label="Em breve" size="small" sx={{ bgcolor: "#5c3600", color: "#fff" }} />
                          )}
                        </Box>
                        <Typography variant="body2" sx={{ color: i === 1 ? "rgba(26,26,26,0.82)" : "rgba(255,255,255,0.78)", mb: 1 }}>
                          Até {plano.maxAlunos} alunos
                        </Typography>
                        <Typography variant="h5" sx={{ fontWeight: 800, color: i === 1 ? "secondary.main" : "primary.main", mt: 1.5 }}>
                          {plano.preco > 0
                            ? <>{formatarBRL(plano.preco)}<Typography component="span" variant="caption" sx={{ fontWeight: 400, ml: 0.5, opacity: 0.92 }}>/mês</Typography></>
                            : "Gratuito"}
                        </Typography>
                        {plano.descricao && (
                          <Box sx={{ mt: 2, display: "flex", alignItems: "center", justifyContent: "center", gap: 0.75 }}>
                            <CheckIcon sx={{ fontSize: 15, color: i === 1 ? "secondary.main" : "primary.main", flexShrink: 0 }} />
                            <Typography variant="caption" sx={{ color: i === 1 ? "rgba(26,26,26,0.82)" : "rgba(255,255,255,0.78)", lineHeight: 1.4 }}>
                              {plano.descricao}
                            </Typography>
                          </Box>
                        )}
                      </CardContent>
                    </Card>
                  );
                  return (
                    <Grid key={plano.planoId} size={{ xs: 12, sm: 6, md: 4 }} sx={{ display: "flex" }}>
                      {isInativo ? card : (
                        <Link href="/cadastro/treinador" style={{ textDecoration: "none", display: "flex", width: "100%" }}>
                          {card}
                        </Link>
                      )}
                    </Grid>
                  );
                })}
              </Grid>
              {/* Single CDC notice outside cards — was repeated per paid card (R8).
                  Guard restores the original preco > 0 scope that was lost when deduplicating from per-card to single notice. */}
              {planos.some(p => p.isAtivo !== false && p.preco > 0) && (
                <Typography
                  variant="caption"
                  sx={{
                    display: "block",
                    textAlign: "center",
                    mt: 4,
                    color: "rgba(255,255,255,0.62)",
                    lineHeight: 1.5,
                    maxWidth: 500,
                    mx: "auto",
                  }}
                >
                  {`Cobrança mensal recorrente. ${CDC_CANCEL_NOTICE}`}
                </Typography>
              )}
            </Container>
          </Box>
        )}

        <Faq />
      </Box>

      <Box
        component="footer"
        sx={{ py: 3, textAlign: "center", borderTop: "1px solid", borderColor: "divider", bgcolor: "background.paper" }}
      >
        <Typography variant="caption" color="text.secondary">
          © {new Date().getFullYear()} forzion.tech — Todos os direitos reservados
        </Typography>
      </Box>
    </Box>
  );
}
