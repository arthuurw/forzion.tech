import { Box, Stack, Typography } from "@mui/material";

interface Props {
  valor: number;
  dense?: boolean;
}

const brl = (v: number) => v.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });

function proximaCobranca(): string {
  const d = new Date();
  d.setMonth(d.getMonth() + 1);
  return d.toLocaleDateString("pt-BR");
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
          Cancelamento gratuito com reembolso integral em até 7 dias da contratação (CDC art. 49).
          Após esse prazo, o cancelamento encerra a renovação sem reembolso do período vigente.
        </Typography>
      </Stack>
    </Box>
  );
}
