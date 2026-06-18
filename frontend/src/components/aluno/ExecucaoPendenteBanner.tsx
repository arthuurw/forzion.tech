"use client";
import { Alert, AlertTitle, Button } from "@mui/material";
import { useExecucaoRetryQueue } from "@/hooks/useExecucaoRetryQueue";

export default function ExecucaoPendenteBanner() {
  const { pendingCount, draining, drain } = useExecucaoRetryQueue();

  if (pendingCount === 0) return null;

  return (
    <Alert
      severity="info"
      role="status"
      sx={{
        mb: 2,
        "& .MuiAlert-action": {
          ml: { xs: 0, sm: "auto" },
          pl: { xs: 0, sm: 2 },
          mt: { xs: 1, sm: 0 },
          width: { xs: "100%", sm: "auto" },
        },
      }}
      action={
        <Button
          color="info"
          size="small"
          variant="outlined"
          disabled={draining}
          onClick={() => void drain()}
          sx={{ width: { xs: "100%", sm: "auto" } }}
        >
          {draining ? "Enviando..." : "Tentar enviar agora"}
        </Button>
      }
    >
      <AlertTitle>
        {pendingCount === 1
          ? "1 treino aguardando envio"
          : `${pendingCount} treinos aguardando envio`}
      </AlertTitle>
      Salvos no aparelho. Serao enviados automaticamente quando voce reconectar.
    </Alert>
  );
}
