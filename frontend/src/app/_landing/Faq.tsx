"use client";
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Box,
  Container,
  Typography,
} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";

const FAQ_ITEMS = [
  {
    question: "Meu aluno precisa pagar para usar?",
    answer:
      "Não à Forzion — quem assina o plano é o treinador. A mensalidade que o aluno paga ao treinador pode ser cobrada pelo app (modo Plataforma, taxa de 5% por transação) ou por fora via Pix/dinheiro (modo Externo, sem cobrança da Forzion).",
  },
  {
    question: "Posso exportar meus dados se cancelar?",
    answer:
      "Sim. Dados exportáveis em Excel ou JSON pela área \"Meus Dados\" nas configurações. Ao cancelar, fichas ficam disponíveis em modo somente-leitura.",
  },
  {
    question: "Funciona no celular?",
    answer:
      "Sim — a plataforma é web responsiva, acessível pelo navegador do celular. Não é app nativo.",
  },
  {
    question: "Posso ter alunos de diferentes objetivos?",
    answer:
      "Sim. Você define um objetivo macro por aluno (hipertrofia, emagrecimento, condicionamento etc.) e monta as fichas adequadas para cada perfil.",
  },
  {
    question: "Como funciona o cancelamento?",
    answer:
      "Pelo Código de Defesa do Consumidor (art. 49): reembolso integral em até 7 dias após a contratação. Após esse prazo, sem reembolso do período vigente. Ao cancelar, suas fichas ficam em modo somente-leitura.",
  },
];

export default function Faq() {
  return (
    <Box sx={{ bgcolor: "background.default", py: { xs: 8, md: 12 } }}>
      <Container maxWidth="md">
        <Box sx={{ textAlign: "center", mb: 8 }}>
          <Typography
            variant="overline"
            sx={{ color: "brand.label", fontWeight: 700, letterSpacing: "0.1em" }}
          >
            FAQ
          </Typography>
          <Typography variant="h4" sx={{ fontWeight: 700, mt: 1 }}>
            Perguntas frequentes
          </Typography>
        </Box>
        {FAQ_ITEMS.map(({ question, answer }) => (
          <Accordion
            key={question}
            disableGutters
            elevation={0}
            // !important overrides MUI's first/last-child border-radius reset on Accordion (higher specificity)
            sx={{ border: "1px solid", borderColor: "divider", mb: 1, borderRadius: "8px !important", "&:before": { display: "none" } }}
          >
            <AccordionSummary expandIcon={<ExpandMoreIcon />}>
              <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
                {question}
              </Typography>
            </AccordionSummary>
            <AccordionDetails>
              <Typography variant="body2" color="text.secondary" sx={{ lineHeight: 1.7 }}>
                {answer}
              </Typography>
            </AccordionDetails>
          </Accordion>
        ))}
      </Container>
    </Box>
  );
}
