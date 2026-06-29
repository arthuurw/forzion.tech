import { Box, Container, Typography, Button } from "@mui/material";
import type { Metadata } from "next";
import Link from "next/link";
import { Suspense } from "react";
import Logo from "@/components/ui/Logo";
import HowItWorks from "./_landing/HowItWorks";
import SocialProof from "./_landing/SocialProof";
import Diferenciais from "./_landing/Diferenciais";
import Faq from "./_landing/Faq";
import PlanosSlab from "./_landing/PlanosSlab";
import PlanosSkeleton from "./_landing/PlanosSkeleton";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";

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

export default function LandingPage() {
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
        <Suspense fallback={<PlanosSkeleton />}>
          <PlanosSlab />
        </Suspense>

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
