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
    nome: "Stripe",
    finalidade: "Processamento de pagamentos e assinaturas recorrentes.",
    dados: "Nome, e-mail e dados de cobrança (tokenizados pela Stripe).",
    local: "Estados Unidos",
  },
  {
    nome: "Resend",
    finalidade: "Envio de e-mails transacionais (verificação, avisos, recuperação de senha).",
    dados: "Nome e endereço de e-mail.",
    local: "Estados Unidos",
  },
  {
    nome: "Supabase",
    finalidade: "Banco de dados (armazenamento de todos os dados da plataforma).",
    dados: "Todas as categorias de dados pessoais tratadas — ver seção anterior.",
    local: "Estados Unidos",
  },
  {
    nome: "Sentry",
    finalidade: "Telemetria de erro e desempenho do backend e do frontend.",
    dados: "Metadados técnicos do erro. Configurado para não coletar dado pessoal.",
    local: "Estados Unidos",
  },
  {
    nome: "Cloudflare R2",
    finalidade: "Armazenamento de backups cifrados do banco de dados.",
    dados: "Backup completo, cifrado em repouso.",
    local: "Estados Unidos",
  },
];

const CATEGORIAS_PII = [
  {
    categoria: "Dados de identificação e contato (nome, e-mail, telefone).",
    baseLegal: "Execução de contrato (art. 7º, V) — necessários para operar a conta.",
  },
  {
    categoria: "Dados de autenticação (hash de senha, fatores de segundo fator).",
    baseLegal: "Execução de contrato (art. 7º, V) — necessários para proteger o acesso à conta.",
  },
  {
    categoria:
      "Dados de saúde declarados na anamnese (condições, restrições e objetivos).",
    baseLegal:
      "Consentimento específico e destacado (art. 11, I) — coletado no cadastro do aluno, revogável a qualquer momento.",
  },
  {
    categoria: "Dados de uso da plataforma (fichas de treino, execuções registradas).",
    baseLegal: "Execução de contrato (art. 7º, V) — é o serviço contratado.",
  },
  {
    categoria: "Dados de pagamento, processados pela Stripe.",
    baseLegal: "Execução de contrato (art. 7º, V).",
  },
  {
    categoria:
      "Dados fiscais do treinador (CPF/CNPJ, razão social, inscrição municipal e endereço fiscal).",
    baseLegal:
      "Cumprimento de obrigação legal ou regulatória (art. 7º, II) — a emissão de documentos fiscais é feita por software fiscal sob responsabilidade do treinador.",
  },
];

export default function PrivacidadePage() {
  return (
    <Box component="main" id="main-content" tabIndex={-1} sx={{ bgcolor: "background.default", minHeight: "100dvh", py: { xs: 4, md: 6 } }}>
      <Container maxWidth="md">
        <Stack spacing={3}>
          <PageHeader title="Política de Privacidade" backHref="/" />

          <Alert severity="info" role="note">
            Versão preliminar, publicada em 2026-08-09. O tratamento de dados
            descrito abaixo já está em vigor; o texto aguarda revisão jurídica
            formal antes de ser considerado definitivo — se a revisão mudar
            algo, este documento será atualizado.
          </Alert>

          <Box component="section">
            <Typography variant="h6" component="h2" gutterBottom sx={{ fontWeight: 600 }}>
              Finalidade do tratamento
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Tratamos seus dados pessoais para operar a plataforma de gestão
              fitness: conectar treinadores e alunos, manter fichas de treino e
              registros de execução, processar assinaturas e pagamentos, cumprir
              obrigações fiscais legais e enviar comunicações essenciais ao
              serviço. O tratamento de dados de saúde ocorre apenas com seu
              consentimento específico (art. 11 da LGPD).
            </Typography>
          </Box>

          <Divider />

          <Box component="section">
            <Typography variant="h6" component="h2" gutterBottom sx={{ fontWeight: 600 }}>
              Categorias de dados pessoais e base legal
            </Typography>
            <Stack component="ul" spacing={1.5} sx={{ pl: 3, m: 0 }}>
              {CATEGORIAS_PII.map((item) => (
                <Typography key={item.categoria} component="li" variant="body2" color="text.secondary">
                  {item.categoria} <strong>Base legal:</strong> {item.baseLegal}
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
              abaixo, que atuam como operadores em nosso nome. O envio de
              notificações por WhatsApp está temporariamente indisponível na
              plataforma — a Meta não consta desta lista enquanto esse canal
              não estiver ativo; a tabela será atualizada quando for reativado.
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
              Os sub-processadores listados acima processam dados nos Estados
              Unidos. Essa transferência internacional se ampara na hipótese do
              art. 33, VII da LGPD — é necessária para a execução do contrato
              de prestação do serviço do qual você é parte, já que a
              infraestrutura que sustenta a plataforma está hospedada nesses
              provedores.
            </Typography>
          </Box>

          <Divider />

          <Box component="section">
            <Typography variant="h6" component="h2" gutterBottom sx={{ fontWeight: 600 }}>
              Retenção e exclusão de dados
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
              A exclusão da conta é feita por anonimização irreversível dos dados
              pessoais. Registros com valor transacional ou fiscal — pagamentos,
              assinaturas e os dados fiscais do treinador — são retidos pelo prazo
              exigido pela legislação fiscal brasileira (cerca de cinco anos),
              mesmo após o pedido de exclusão, com base no cumprimento de
              obrigação legal e regulatória (art. 7º, II e art. 16, I da LGPD).
              Encerrado esse prazo, são anonimizados automaticamente por rotina
              mensal.
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Outras retenções específicas: tokens de troca de e-mail são
              apagados assim que expiram ou são usados; o conteúdo de
              notificações é apagado após 90 dias. Nenhuma dessas retenções
              afeta o prazo de resposta ao seu pedido de exclusão.
            </Typography>
          </Box>

          <Divider />

          <Box component="section">
            <Typography variant="h6" component="h2" gutterBottom sx={{ fontWeight: 600 }}>
              Encarregado de dados (DPO) e seus direitos
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
              A forzion.tech é operada por um responsável único, que também
              atua como encarregado de dados (art. 41 da LGPD). Para exercer
              qualquer direito do art. 18 — acesso, correção, portabilidade,
              exclusão ou revogação de consentimento — entre em contato pelo
              e-mail <strong>suporte@forzion.tech</strong>.
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
              Solicitações de <strong>exportação</strong> e{" "}
              <strong>exclusão</strong> têm mecanismo próprio na plataforma —
              área de Privacidade (LGPD) no seu perfil — e não precisam passar
              pelo e-mail do encarregado; use o canal apenas se preferir
              atendimento assistido ou se o mecanismo automático não
              atender seu caso. Se sua conta for de treinador com vínculo
              ativo com alunos, a exclusão automática é bloqueada até o
              vínculo ser encerrado — o canal do encarregado explica os
              próximos passos nesse caso.
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
              Se o seu pedido for de exclusão e você tiver dado fiscal ou
              transacional retido (seção anterior), a resposta explicará que a
              exclusão ocorre por anonimização imediata dos dados pessoais,
              com o dado financeiro retido pelo prazo fiscal — o registro
              deixa de ser associável a você, mas não é apagado antes do
              prazo legal.
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Solicitações são respondidas em até 15 dias (art. 19, LGPD). Se a
              solicitação vier de terceiro alegando representar o titular,
              exigimos comprovação antes de agir, para não transformar este
              canal em vetor de engenharia social contra a conta de outra
              pessoa.
            </Typography>
          </Box>
        </Stack>
      </Container>
    </Box>
  );
}
