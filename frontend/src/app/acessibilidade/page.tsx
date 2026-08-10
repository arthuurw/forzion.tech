import { Box, Container, Typography, Stack, Divider } from "@mui/material";
import PageHeader from "@/components/ui/PageHeader";

export const metadata = {
  title: "Acessibilidade — forzion.tech",
  description:
    "Compromisso de acessibilidade da forzion.tech: padrão perseguido, estado de conformidade WCAG 2.1 AA e canal de contato para relato de barreiras.",
  alternates: { canonical: "/acessibilidade" },
};

export default function AcessibilidadePage() {
  return (
    <Box component="main" id="main-content" tabIndex={-1} sx={{ bgcolor: "background.default", minHeight: "100dvh", py: { xs: 4, md: 6 } }}>
      <Container maxWidth="md">
        <Stack spacing={3}>
          <PageHeader title="Acessibilidade" backHref="/" />

          <Box component="section">
            <Typography variant="h6" component="h2" gutterBottom sx={{ fontWeight: 600 }}>
              Padrão perseguido, data e escopo
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Perseguimos o padrão <strong>WCAG 2.1, nível AA</strong>. A
              última avaliação de conformidade foi compilada em{" "}
              <strong>2026-08-09</strong>, cobrindo rotas públicas amostradas
              (login) e os fluxos autenticados de aluno de cadastro e de
              registro de execução de treino — os três fluxos considerados
              mais críticos da plataforma.
            </Typography>
          </Box>

          <Divider />

          <Box component="section">
            <Typography variant="h6" component="h2" gutterBottom sx={{ fontWeight: 600 }}>
              Estado de conformidade
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
              O percurso manual com leitores de tela NVDA (Windows) e
              VoiceOver (macOS/iOS), nos três fluxos críticos e nos critérios
              de qualidade de texto alternativo, hierarquia de cabeçalhos e
              idioma do documento, não encontrou nenhum achado — todos
              conformes.
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Os critérios verificados por automação de navegador (ordem de
              foco lógica, foco visível, navegação por teclado sem armadilha,
              reflow a 320px, zoom a 200% e respeito a movimento reduzido)
              aguardam confirmação de execução no ambiente de integração
              contínua.
            </Typography>
          </Box>

          <Divider />

          <Box component="section">
            <Typography variant="h6" component="h2" gutterBottom sx={{ fontWeight: 600 }}>
              Não-conformidades conhecidas
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Nenhuma não-conformidade foi encontrada até o momento. Os
              critérios verificados por automação de navegador, listados na
              seção anterior, seguem pendentes de confirmação no ambiente de
              integração contínua; se essa confirmação revelar alguma falha,
              esta página será atualizada com a previsão de correção.
            </Typography>
          </Box>

          <Divider />

          <Box component="section">
            <Typography variant="h6" component="h2" gutterBottom sx={{ fontWeight: 600 }}>
              Relatar uma barreira
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Se você encontrou uma barreira de acessibilidade na plataforma,
              entre em contato pelo e-mail{" "}
              <strong>suporte@forzion.tech</strong>. Solicitações são
              respondidas em até 15 dias.
            </Typography>
          </Box>
        </Stack>
      </Container>
    </Box>
  );
}
