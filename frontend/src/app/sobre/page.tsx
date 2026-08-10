import { Box, Container, Typography, Stack, Divider } from "@mui/material";
import Link from "next/link";
import PageHeader from "@/components/ui/PageHeader";
import { POR_QUE_EXISTIMOS } from "./content";

export const metadata = {
  title: "Sobre — forzion.tech",
  description:
    "Quem opera a forzion.tech, por que a plataforma existe, a quem ela atende e como falar com o encarregado de dados.",
  alternates: { canonical: "/sobre" },
};

export default function SobrePage() {
  return (
    <Box component="main" id="main-content" tabIndex={-1} sx={{ bgcolor: "background.default", minHeight: "100dvh", py: { xs: 4, md: 6 } }}>
      <Container maxWidth="md">
        <Stack spacing={3}>
          <PageHeader title="Sobre a FORZION.TECH" backHref="/" />

          <Box component="section">
            <Typography variant="h6" component="h2" gutterBottom sx={{ fontWeight: 600 }}>
              Quem somos
            </Typography>
            <Typography variant="body2" color="text.secondary">
              FORZION.TECH é uma marca da FORZIONTECH DESENVOLVIMENTO DE
              SOFTWARE CUSTOMIZAVEL LTDA (CNPJ 67.900.114/0001-69), em
              operação desde julho de 2026.
            </Typography>
          </Box>

          <Divider />

          <Box component="section">
            <Typography variant="h6" component="h2" gutterBottom sx={{ fontWeight: 600 }}>
              Por que existimos
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {POR_QUE_EXISTIMOS}
            </Typography>
          </Box>

          <Divider />

          <Box component="section">
            <Typography variant="h6" component="h2" gutterBottom sx={{ fontWeight: 600 }}>
              A quem atendemos
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Feita principalmente para treinadores autônomos, mas aberta a
              qualquer profissional ou equipe que queira gerenciar alunos,
              fichas de treino e pagamentos num só lugar.
            </Typography>
          </Box>

          <Divider />

          <Box component="section">
            <Typography variant="h6" component="h2" gutterBottom sx={{ fontWeight: 600 }}>
              Encarregado de dados
            </Typography>
            <Typography variant="body2" color="text.secondary">
              A forzion.tech é operada por um responsável único, que também
              atua como encarregado de dados. Para exercer qualquer direito
              sobre seus dados pessoais, entre em contato pelo e-mail{" "}
              <strong>suporte@forzion.tech</strong>. Detalhes completos sobre
              tratamento de dados, prazos e seus direitos estão na{" "}
              <Link href="/privacidade">Política de Privacidade</Link>.
            </Typography>
          </Box>

          <Divider />

          <Box component="section">
            <Typography variant="h6" component="h2" gutterBottom sx={{ fontWeight: 600 }}>
              Saiba mais
            </Typography>
            <Typography variant="body2" color="text.secondary">
              <Link href="/privacidade">Política de Privacidade</Link>
              {" · "}
              <Link href="/acessibilidade">Acessibilidade</Link>
            </Typography>
          </Box>
        </Stack>
      </Container>
    </Box>
  );
}
