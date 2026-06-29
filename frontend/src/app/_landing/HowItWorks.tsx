import { Box, Container, Typography, Grid } from "@mui/material";
import SectionEyebrow from "./SectionEyebrow";
import StepMockup, { type StepVariant } from "./StepMockup";

const STEPS: { step: string; title: string; description: string; variant: StepVariant }[] = [
  {
    step: "01",
    title: "Prescrição estruturada",
    description:
      "Monte fichas com exercícios, séries e cargas. Personalize por objetivo e adapte conforme a evolução de cada aluno.",
    variant: "ficha",
  },
  {
    step: "02",
    title: "Gestão da carteira de alunos",
    description:
      "Controle quem acessa o quê. Vincule, aprove e organize sua carteira com critério e agilidade.",
    variant: "alunos",
  },
  {
    step: "03",
    title: "Histórico de execuções",
    description:
      "Cada sessão gera dados reais. Use o histórico para ajustar protocolos e demonstrar evolução ao aluno.",
    variant: "historico",
  },
];

export default function HowItWorks() {
  return (
    <Box sx={{ bgcolor: "background.default", py: { xs: 8, md: 12 } }}>
      <Container maxWidth="lg">
        <Box sx={{ textAlign: "center", mb: 8 }}>
          <SectionEyebrow label="COMO FUNCIONA" variant="light" />
          <Typography variant="h4" sx={{ mt: 2 }}>
            Uma estrutura pensada para o dia a dia
          </Typography>
        </Box>
        <Box sx={{ display: "flex", flexDirection: "column", gap: { xs: 8, md: 10 } }}>
          {STEPS.map(({ step, title, description, variant }, i) => (
            <Grid
              key={step}
              container
              spacing={6}
              sx={{
                alignItems: "center",
                flexDirection: {
                  xs: "column",
                  md: i % 2 === 0 ? "row" : "row-reverse",
                },
              }}
            >
              <Grid size={{ xs: 12, md: 6 }}>
                <Box
                  sx={{
                    width: "100%",
                    aspectRatio: "1280/900",
                    borderRadius: 3,
                    overflow: "hidden",
                    boxShadow: "0 8px 40px rgba(0,0,0,0.12)",
                  }}
                >
                  <StepMockup variant={variant} />
                </Box>
              </Grid>
              <Grid size={{ xs: 12, md: 6 }}>
                <Typography
                  variant="overline"
                  sx={{
                    color: "brand.label",
                    fontWeight: 800,
                    fontSize: "1.1rem",
                    letterSpacing: "0.05em",
                  }}
                >
                  {step}
                </Typography>
                <Typography variant="h5" sx={{ mt: 1, mb: 2 }}>
                  {title}
                </Typography>
                <Typography variant="body1" color="text.secondary" sx={{ lineHeight: 1.8 }}>
                  {description}
                </Typography>
              </Grid>
            </Grid>
          ))}
        </Box>
      </Container>
    </Box>
  );
}
