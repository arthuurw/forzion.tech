"use client";
import { Alert, AlertTitle, Button } from "@mui/material";
import Link from "next/link";

interface AlunoInadimplenteBannerProps {
  /**
   * Severidade visual do alerta.
   * - "warning" (default): tom de atencao, nao bloqueante visualmente.
   * - "error": uso quando contexto pede destaque maior.
   */
  variant?: "warning" | "error";
}

/**
 * Banner persistente exibido no layout do aluno quando a assinatura esta
 * Inadimplente. Nao possui onClose: visivel ate o backend reportar status != Inadimplente.
 *
 * CTA leva o aluno para /aluno/pagamentos onde pode gerar nova cobranca.
 */
export default function AlunoInadimplenteBanner({
  variant = "warning",
}: AlunoInadimplenteBannerProps) {
  return (
    <Alert
      severity={variant}
      role="alert"
      sx={{
        mb: 2,
        // xs: CTA abaixo do texto (largura cheia) em vez de espremido à direita.
        "& .MuiAlert-action": {
          ml: { xs: 0, sm: "auto" },
          pl: { xs: 0, sm: 2 },
          mt: { xs: 1, sm: 0 },
          width: { xs: "100%", sm: "auto" },
        },
      }}
      action={
        <Button
          component={Link}
          href="/aluno/pagamentos"
          color={variant}
          size="small"
          variant="outlined"
          sx={{ width: { xs: "100%", sm: "auto" } }}
        >
          Regularizar agora
        </Button>
      }
    >
      <AlertTitle>Assinatura inadimplente</AlertTitle>
      Sua assinatura esta inadimplente. Regularize seu pagamento para liberar
      acesso completo as fichas e execucoes.
    </Alert>
  );
}
