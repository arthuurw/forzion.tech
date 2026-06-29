import {
  Box,
  Container,
  Typography,
  Stack,
  Divider,
  // eslint-disable-next-line no-restricted-imports -- disclaimer estático role="note" (não-dismissível); AlertBanner é Collapse dismissível
  Alert,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
} from "@mui/material";
import PageHeader from "@/components/ui/PageHeader";

export const metadata = {
  title: "Política de Privacidade — forzion.tech",
  description:
    "Como a forzion.tech trata seus dados pessoais: finalidade, categorias, sub-processadores e transferência internacional.",
};

const SUBPROCESSADORES = [
  {
    nome: "Resend",
    finalidade: "Envio de e-mails transacionais (verificação, avisos, recuperação de senha).",
    dados: "Nome e endereço de e-mail.",
    local: "Estados Unidos",
  },
  {
    nome: "Stripe",
    finalidade: "Processamento de pagamentos e assinaturas recorrentes.",
    dados: "Nome, e-mail e dados de cobrança (tokenizados pela Stripe).",
    local: "Estados Unidos / União Europeia",
  },
  {
    nome: "Meta (WhatsApp Cloud API)",
    finalidade: "Notificações operacionais via WhatsApp, quando habilitado.",
    dados: "Número de telefone e conteúdo da notificação.",
    local: "Estados Unidos",
  },
];

const CATEGORIAS_PII = [
  "Dados de identificação e contato (nome, e-mail, telefone).",
  "Dados de autenticação (hash de senha, fatores de segundo fator).",
  "Dados de saúde declarados na anamnese (condições, restrições e objetivos), tratados mediante consentimento específico.",
  "Dados de uso da plataforma (fichas de treino, execuções registradas).",
  "Dados de pagamento, processados pela Stripe.",
];

export default function PrivacidadePage() {
  return (
    <Box component="main" id="main-content" tabIndex={-1} sx={{ bgcolor: "background.default", minHeight: "100dvh", py: { xs: 4, md: 6 } }}>
      <Container maxWidth="md">
        <Stack spacing={3}>
          <PageHeader title="Política de Privacidade" backHref="/" />

          <Alert severity="info" role="note">
            Documento preliminar. As seções abaixo descrevem o tratamento de
            dados atualmente em vigor; o texto jurídico definitivo ainda está em
            elaboração e não constitui a versão final.
          </Alert>

          <Box component="section">
            <Typography variant="h6" component="h2" gutterBottom sx={{ fontWeight: 600 }}>
              Finalidade do tratamento
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Tratamos seus dados pessoais para operar a plataforma de gestão
              fitness: conectar treinadores e alunos, manter fichas de treino e
              registros de execução, processar assinaturas e pagamentos, e enviar
              comunicações essenciais ao serviço. O tratamento de dados de saúde
              ocorre apenas com seu consentimento específico (art. 11 da LGPD).
            </Typography>
          </Box>

          <Divider />

          <Box component="section">
            <Typography variant="h6" component="h2" gutterBottom sx={{ fontWeight: 600 }}>
              Categorias de dados pessoais
            </Typography>
            <Stack component="ul" spacing={1} sx={{ pl: 3, m: 0 }}>
              {CATEGORIAS_PII.map((item) => (
                <Typography key={item} component="li" variant="body2" color="text.secondary">
                  {item}
                </Typography>
              ))}
            </Stack>
          </Box>

          <Divider />

          <Box component="section">
            <Typography variant="h6" component="h2" gutterBottom sx={{ fontWeight: 600 }}>
              Sub-processadores
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
              Compartilhamos dados estritamente necessários com os provedores
              abaixo, que atuam como operadores em nosso nome:
            </Typography>
            <TableContainer component={Paper} variant="outlined">
              <Table size="small" aria-label="Sub-processadores de dados">
                <TableHead>
                  <TableRow>
                    <TableCell>Sub-processador</TableCell>
                    <TableCell>Finalidade</TableCell>
                    <TableCell>Dados compartilhados</TableCell>
                    <TableCell>Local de processamento</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {SUBPROCESSADORES.map((sp) => (
                    <TableRow key={sp.nome}>
                      <TableCell sx={{ fontWeight: 600 }}>{sp.nome}</TableCell>
                      <TableCell>{sp.finalidade}</TableCell>
                      <TableCell>{sp.dados}</TableCell>
                      <TableCell>{sp.local}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          </Box>

          <Divider />

          <Box component="section">
            <Typography variant="h6" component="h2" gutterBottom sx={{ fontWeight: 600 }}>
              Transferência internacional de dados
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Os sub-processadores listados processam dados fora do Brasil
              (Estados Unidos e União Europeia). Essa transferência internacional
              ocorre com base nas hipóteses do art. 33 da LGPD e em cláusulas
              contratuais que asseguram nível de proteção adequado. O texto
              jurídico definitivo detalhará a base legal específica de cada
              transferência.
            </Typography>
          </Box>

          <Divider />

          <Box component="section">
            <Typography variant="h6" component="h2" gutterBottom sx={{ fontWeight: 600 }}>
              Seus direitos e contato
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Você pode solicitar acesso, correção, portabilidade ou exclusão dos
              seus dados, e revogar consentimentos, pela área de Privacidade
              (LGPD) no seu perfil. O canal do encarregado (DPO) será divulgado na
              versão definitiva deste documento.
            </Typography>
          </Box>
        </Stack>
      </Container>
    </Box>
  );
}
