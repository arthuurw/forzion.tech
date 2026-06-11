import { Box, Stack, Typography } from "@mui/material";
import { CDC_CANCEL_NOTICE } from "@/lib/constants/billing";
import { proximaCobranca } from "@/lib/utils/billing";

interface Props {
  valor: number;
  dense?: boolean;
}

const brl = (v: number) => v.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });

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
