"use client";
import { Box, Container, Typography, Grid } from "@mui/material";
import FitnessCenterIcon from "@mui/icons-material/FitnessCenter";
import GroupIcon from "@mui/icons-material/Group";
import TrackChangesIcon from "@mui/icons-material/TrackChanges";

const STEPS = [
  {
    Icon: FitnessCenterIcon,
    title: "Treinador cria fichas",
    description:
      "O personal monta fichas personalizadas com exercícios, séries e cargas para cada objetivo.",
  },
  {
    Icon: GroupIcon,
    title: "Aluno é vinculado",
    description:
      "O aluno se cadastra e escolhe o treinador. Após aprovação, recebe acesso às fichas.",
  },
  {
    Icon: TrackChangesIcon,
    title: "Evolução registrada",
    description:
      "A cada treino executado, o histórico fica disponível para acompanhamento contínuo.",
  },
];

export default function HowItWorks() {
  return (
    <Container maxWidth="md" sx={{ py: { xs: 6, md: 10 } }}>
      <Typography variant="h4" sx={{ fontWeight: 700, textAlign: "center", mb: 6 }}>
        Como funciona
      </Typography>
      <Grid container spacing={4}>
        {STEPS.map(({ Icon, title, description }, i) => (
          <Grid key={i} size={{ xs: 12, md: 4 }}>
            <Box sx={{ textAlign: "center" }}>
              <Box
                sx={{
                  width: 64,
                  height: 64,
                  borderRadius: "50%",
                  bgcolor: "primary.main",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  mx: "auto",
                  mb: 2,
                }}
              >
                <Icon sx={{ fontSize: 32, color: "secondary.main" }} />
              </Box>
              <Typography variant="h6" sx={{ fontWeight: 600, mb: 1 }}>
                {title}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {description}
              </Typography>
            </Box>
          </Grid>
        ))}
      </Grid>
    </Container>
  );
}
