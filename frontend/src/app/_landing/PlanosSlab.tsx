import { Box, Container, Typography, Grid, Card, CardContent, Chip } from "@mui/material";
import Link from "next/link";
import CheckIcon from "@mui/icons-material/Check";
import * as Sentry from "@sentry/nextjs";
import type { PlanoPlataformaResponse } from "@/types";
import { CDC_CANCEL_NOTICE } from "@/lib/constants/billing";
import { formatarBRL } from "@/lib/utils/formatting";

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

export default async function PlanosSlab() {
  const planos = await getPlanos();

  if (planos.length === 0) return null;

  return (
    <Box sx={{ bgcolor: "secondary.main", py: { xs: 6, md: 8 } }}>
      <Container maxWidth="md">
        <Box sx={{ textAlign: "center", mb: 6 }}>
          <Typography variant="h4" sx={{ mb: 1, color: "white" }}>
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
  );
}
