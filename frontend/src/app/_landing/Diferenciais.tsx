import { Box, Container, Typography, Grid } from "@mui/material";
import CheckIcon from "@mui/icons-material/Check";
import CloseIcon from "@mui/icons-material/Close";

const ROWS = [
  {
    aspecto: "Foco",
    forzion: "Desenvolvido para personal trainer",
    generica: "Para qualquer academia ou modalidade",
  },
  {
    aspecto: "Histórico",
    forzion: "Por execução: séries, cargas e datas de cada treino",
    generica: "Por ficha (sem granularidade por sessão)",
  },
  {
    aspecto: "Plano gratuito",
    forzion: "Permanente (até 10 alunos)",
    generica: "Trial ou freemium com vencimento",
  },
  {
    aspecto: "Acesso de alunos",
    forzion: "Controle individual por aluno",
    generica: "Sem gestão de acesso por aluno",
  },
];

export default function Diferenciais() {
  return (
    <Box sx={{ bgcolor: "background.paper", py: { xs: 8, md: 12 } }}>
      <Container maxWidth="md">
        <Box sx={{ textAlign: "center", mb: 8 }}>
          <Typography variant="overline" sx={{ color: "brand.label", fontWeight: 700, letterSpacing: "0.1em" }}>
            DIFERENCIAIS
          </Typography>
          <Typography variant="h4" sx={{ fontWeight: 700, mt: 1 }}>
            O que nos diferencia
          </Typography>
        </Box>

        <Grid container sx={{ mb: 1, px: { xs: 1, md: 2 } }}>
          <Grid size={{ xs: 4, md: 4 }} />
          <Grid size={{ xs: 4, md: 4 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 700, textAlign: "center", color: "primary.main" }}>
              Forzion
            </Typography>
          </Grid>
          <Grid size={{ xs: 4, md: 4 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 700, textAlign: "center", color: "text.secondary" }}>
              Ferramentas genéricas
            </Typography>
          </Grid>
        </Grid>

        {ROWS.map(({ aspecto, forzion, generica }, i) => (
          <Grid
            key={aspecto}
            container
            sx={{
              alignItems: "flex-start",
              px: { xs: 1, md: 2 },
              py: 2,
              borderTop: "1px solid",
              borderColor: "divider",
              // Alternate background to aid scannability without relying on hover state
              bgcolor: i % 2 === 0 ? "background.default" : "background.paper",
              borderRadius: 1,
            }}
          >
            <Grid size={{ xs: 4, md: 4 }}>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {aspecto}
              </Typography>
            </Grid>
            <Grid size={{ xs: 4, md: 4 }}>
              <Box sx={{ display: "flex", alignItems: "flex-start", gap: 0.75, justifyContent: "center" }}>
                <CheckIcon sx={{ fontSize: 18, color: "success.main", flexShrink: 0, mt: "1px" }} />
                <Typography variant="body2" color="text.primary" sx={{ textAlign: "left" }}>
                  {forzion}
                </Typography>
              </Box>
            </Grid>
            <Grid size={{ xs: 4, md: 4 }}>
              <Box sx={{ display: "flex", alignItems: "flex-start", gap: 0.75, justifyContent: "center" }}>
                <CloseIcon sx={{ fontSize: 18, color: "text.disabled", flexShrink: 0, mt: "1px" }} />
                <Typography variant="body2" color="text.secondary" sx={{ textAlign: "left" }}>
                  {generica}
                </Typography>
              </Box>
            </Grid>
          </Grid>
        ))}
      </Container>
    </Box>
  );
}
