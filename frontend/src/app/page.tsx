import { Box, Container, Typography, Button, Grid, Card, CardContent, Divider } from "@mui/material";
import Link from "next/link";
import Logo from "@/components/ui/Logo";
import HowItWorks from "./_landing/HowItWorks";
import type { PlanoTreinadorResponse } from "@/types";

async function getPlanos(): Promise<PlanoTreinadorResponse[]> {
  try {
    const base = process.env.API_BASE_URL ?? "https://localhost:7220";
    const res = await fetch(`${base}/auth/planos`, { next: { revalidate: 3600 } });
    if (!res.ok) return [];
    return res.json();
  } catch {
    return [];
  }
}

export default async function LandingPage() {
  const planos = await getPlanos();

  return (
    <Box sx={{ display: "flex", flexDirection: "column", minHeight: "100vh" }}>
      {/* Header */}
      <Box
        component="header"
        sx={{
          py: 2,
          px: 3,
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          borderBottom: "1px solid",
          borderColor: "divider",
          bgcolor: "background.paper",
        }}
      >
        <Logo size="md" />
        <Box sx={{ display: "flex", gap: 1 }}>
          <Link href="/login" style={{ textDecoration: "none" }}>
            <Button variant="outlined" color="secondary" size="small">Entrar</Button>
          </Link>
          <Link href="/cadastro/treinador" style={{ textDecoration: "none" }}>
            <Button variant="contained" color="primary" size="small">Começar grátis</Button>
          </Link>
        </Box>
      </Box>

      <Box component="main" sx={{ flex: 1 }}>
        {/* Hero */}
        <Box sx={{ bgcolor: "secondary.main", color: "white", py: { xs: 8, md: 12 }, textAlign: "center" }}>
          <Container maxWidth="md">
            <Typography
              variant="h3"
              sx={{ mb: 2, fontWeight: 700, fontSize: { xs: "2rem", md: "2.75rem" } }}
            >
              Gestão de treinos para{" "}
              <Box component="span" sx={{ color: "primary.main" }}>
                personal trainers
              </Box>
            </Typography>
            <Typography variant="h6" sx={{ opacity: 0.8, mb: 4, fontWeight: 400 }}>
              Crie fichas, acompanhe alunos e registre evoluções — tudo em um só lugar.
            </Typography>
            <Box sx={{ display: "flex", gap: 2, justifyContent: "center", flexWrap: "wrap" }}>
              <Link href="/cadastro/treinador" style={{ textDecoration: "none" }}>
                <Button variant="contained" color="primary" size="large">
                  Criar conta como treinador
                </Button>
              </Link>
              <Link href="/cadastro/aluno" style={{ textDecoration: "none" }}>
                <Button variant="outlined" size="large" sx={{ borderColor: "white", color: "white" }}>
                  Sou aluno
                </Button>
              </Link>
            </Box>
          </Container>
        </Box>

        {/* Como funciona */}
        <HowItWorks />

        {/* Planos */}
        {planos.length > 0 && (
          <>
            <Divider />
            <Container maxWidth="md" sx={{ py: { xs: 6, md: 10 } }}>
              <Typography variant="h4" sx={{ fontWeight: 700, textAlign: "center", mb: 6 }}>
                Planos disponíveis
              </Typography>
              <Grid container spacing={3} sx={{ justifyContent: "center" }}>
                {planos.map((plano) => (
                  <Grid key={plano.planoId} size={{ xs: 12, sm: 6, md: 4 }}>
                    <Card variant="outlined" sx={{ textAlign: "center", p: 1 }}>
                      <CardContent>
                        <Typography variant="h6" sx={{ fontWeight: 700, mb: 1 }}>
                          {plano.nome}
                        </Typography>
                        <Typography variant="body2" color="text.secondary">
                          Até {plano.maxAlunos} alunos
                        </Typography>
                        <Link href="/cadastro/treinador" style={{ textDecoration: "none" }}>
                          <Button variant="contained" color="primary" fullWidth sx={{ mt: 3 }}>
                            Escolher
                          </Button>
                        </Link>
                      </CardContent>
                    </Card>
                  </Grid>
                ))}
              </Grid>
            </Container>
          </>
        )}
      </Box>

      {/* Footer */}
      <Box
        component="footer"
        sx={{ py: 2, textAlign: "center", borderTop: "1px solid", borderColor: "divider" }}
      >
        <Typography variant="caption" color="text.secondary">
          © {new Date().getFullYear()} forzion.tech — Todos os direitos reservados
        </Typography>
      </Box>
    </Box>
  );
}
