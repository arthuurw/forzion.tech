"use client";
import { Box, Container, Typography, Grid } from "@mui/material";
import FitnessCenterIcon from "@mui/icons-material/FitnessCenter";
import GroupIcon from "@mui/icons-material/Group";
import TrackChangesIcon from "@mui/icons-material/TrackChanges";

const STEPS = [
  {
    Icon: FitnessCenterIcon,
    step: "01",
    title: "Prescrição estruturada",
    description: "Monte fichas com exercícios, séries e cargas. Personalize por objetivo e adapte conforme a evolução de cada aluno.",
  },
  {
    Icon: GroupIcon,
    step: "02",
    title: "Gestão da carteira de alunos",
    description: "Controle quem acessa o quê. Vincule, aprove e organize sua carteira com critério e agilidade.",
  },
  {
    Icon: TrackChangesIcon,
    step: "03",
    title: "Histórico de execuções",
    description: "Cada sessão gera dados reais. Use o histórico para ajustar protocolos e demonstrar evolução ao aluno.",
  },
];

export default function HowItWorks() {
  return (
    <Box sx={{ bgcolor: "background.default", py: { xs: 8, md: 12 } }}>
      <Container maxWidth="md">
        <Box sx={{ textAlign: "center", mb: 8 }}>
          <Typography variant="overline" sx={{ color: "#7a6300", fontWeight: 700, letterSpacing: "0.1em" }}>
            COMO FUNCIONA
          </Typography>
          <Typography variant="h4" sx={{ fontWeight: 700, mt: 1 }}>
            Uma estrutura pensada para o dia a dia
          </Typography>
        </Box>
        <Grid container spacing={4}>
          {STEPS.map(({ Icon, step, title, description }) => (
            <Grid key={step} size={{ xs: 12, md: 4 }}>
              <Box
                sx={{
                  bgcolor: "background.paper",
                  borderRadius: 4,
                  p: 3.5,
                  height: "100%",
                  border: "1px solid",
                  borderColor: "divider",
                  transition: "box-shadow 0.2s, transform 0.2s",
                  "&:hover": {
                    boxShadow: "0 8px 32px rgba(0,0,0,0.1)",
                    transform: "translateY(-2px)",
                  },
                }}
              >
                <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 2.5 }}>
                  <Box
                    sx={{
                      width: 48,
                      height: 48,
                      borderRadius: 2.5,
                      bgcolor: "primary.main",
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                    }}
                  >
                    <Icon sx={{ fontSize: 26, color: "secondary.main" }} />
                  </Box>
                  <Typography variant="h5" sx={{ fontWeight: 800, color: "#808080", letterSpacing: "-0.02em" }}>
                    {step}
                  </Typography>
                </Box>
                <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 1 }}>
                  {title}
                </Typography>
                <Typography variant="body2" color="text.secondary" sx={{ lineHeight: 1.7 }}>
                  {description}
                </Typography>
              </Box>
            </Grid>
          ))}
        </Grid>
      </Container>
    </Box>
  );
}
