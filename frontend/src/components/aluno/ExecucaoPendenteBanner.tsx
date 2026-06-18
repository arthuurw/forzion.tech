"use client";
import { useCallback, useState } from "react";
import { Alert, AlertTitle, Button, Stack } from "@mui/material";
import {
  useExecucaoRetryQueue,
  type RetryQueueFailure,
} from "@/hooks/useExecucaoRetryQueue";

export default function ExecucaoPendenteBanner() {
  const [descartado, setDescartado] = useState<string | null>(null);

  const onError = useCallback((failure: RetryQueueFailure) => {
    setDescartado(failure.message ?? "O registro foi recusado pelo servidor.");
  }, []);

  const { pendingCount, draining, drain } = useExecucaoRetryQueue({ onError });

  if (pendingCount === 0 && descartado === null) return null;

  return (
    <Stack spacing={2} sx={{ mb: 2 }}>
      {descartado !== null && (
        <Alert severity="error" role="alert" onClose={() => setDescartado(null)}>
          <AlertTitle>Um treino não pôde ser enviado</AlertTitle>
          {descartado}
        </Alert>
      )}
      {pendingCount > 0 && (
        <Alert
          severity="info"
          role="status"
          sx={{
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
      )}
    </Stack>
  );
}
