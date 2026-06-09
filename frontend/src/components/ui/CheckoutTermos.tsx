import { Box, Stack, Typography } from "@mui/material";
import { CDC_CANCEL_NOTICE } from "@/lib/constants/billing";

interface Props {
  valor: number;
  dense?: boolean;
}

const brl = (v: number) => v.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });

export function proximaCobranca(hoje: Date = new Date()): string {
  // setMonth(+1) faz overflow em meses curtos (31/jan → 03/mar); clampa ao último dia do mês alvo.
  const alvo = new Date(hoje.getFullYear(), hoje.getMonth() + 1, 1);
  const ultimoDia = new Date(alvo.getFullYear(), alvo.getMonth() + 1, 0).getDate();
  alvo.setDate(Math.min(hoje.getDate(), ultimoDia));
  return alvo.toLocaleDateString("pt-BR");
}

export default function CheckoutTermos({ valor, dense }: Props) {
  return (
    <Box
      sx={{
        p: dense ? 1.5 : 2,
        borderRadius: 1,
        bgcolor: "action.hover",
        border: "1px solid",
        borderColor: "divider",
      }}
    >
      <Stack spacing={0.5}>
        <Typography variant="body2">
          <strong>Valor:</strong> {brl(valor)} — cobrança <strong>mensal</strong> recorrente.
        </Typography>
        <Typography variant="body2">
          <strong>Próxima cobrança:</strong> estimada para {proximaCobranca()}.
        </Typography>
        <Typography variant="caption" color="text.secondary">
          {CDC_CANCEL_NOTICE}
        </Typography>
      </Stack>
    </Box>
  );
}
