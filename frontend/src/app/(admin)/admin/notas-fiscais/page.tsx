"use client";
import { useCallback, useState } from "react";
import {
  Box, Typography, Card, CardContent, Chip, Button, IconButton, Tooltip,
  TextField, MenuItem, Stack,
} from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import ReceiptLongIcon from "@mui/icons-material/ReceiptLong";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import { ResponsiveTable, type Column } from "@/components/ui/ResponsiveTable";
import {
  nfseApi, type NotaFiscalAdmin, type NotaFiscalStatus,
  NOTA_FISCAL_STATUS_LABEL, NOTA_FISCAL_STATUS_COLOR, TIPO_NOTA_FISCAL_LABEL,
} from "@/lib/api/nfse";
import { extractApiError } from "@/lib/api/extractApiError";
import { formatarBRL, formatarDataHora } from "@/lib/utils/formatting";
import { useCursorList } from "@/hooks/useCursorList";

const COLUMNS: Column[] = [
  { label: "Treinador" },
  { label: "Tipo" },
  { label: "Emissão" },
  { label: "Valor", align: "right" },
  { label: "Status" },
  { label: "Erro" },
  { label: "Ações", align: "right" },
];

const STATUS_FILTROS: (NotaFiscalStatus | "")[] = [
  "", "Pendente", "Emitida", "Erro", "BloqueadaDadosFiscais",
  "CancelamentoSolicitado", "Cancelada", "CancelamentoExpirado",
];

export default function NotasFiscaisAdminPage() {
  const [statusFiltro, setStatusFiltro] = useState<NotaFiscalStatus | "">("");
  const [reprocessando, setReprocessando] = useState<string | null>(null);
  const [success, setSuccess] = useState("");

  const fetcher = useCallback(
    (filtro: NotaFiscalStatus | "", aposId: string | undefined, signal: AbortSignal) =>
      nfseApi.listNotasAdmin({ status: filtro || undefined, aposId }, signal),
    [],
  );

  const {
    itens: notas, cursor, loading, loadingMais, error, setError, carregarMais, reload,
  } = useCursorList<NotaFiscalAdmin, NotaFiscalStatus | "">({
    fetcher,
    filtro: statusFiltro,
    errorMessage: "Erro ao carregar notas fiscais.",
  });

  const reprocessar = async (nota: NotaFiscalAdmin) => {
    setError("");
    setSuccess("");
    setReprocessando(nota.id);
    try {
      await nfseApi.reprocessarNota(nota.id);
      setSuccess("Emissão reenfileirada.");
      reload();
    } catch (err) {
      setError(extractApiError(err, "Não foi possível reprocessar a nota."));
    } finally {
      setReprocessando(null);
    }
  };

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 3 }}>
        <ReceiptLongIcon sx={{ color: "text.secondary" }} />
        <Typography variant="h5" sx={{ fontWeight: 700 }}>Notas fiscais</Typography>
      </Box>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />
      <AlertBanner open={!!success} severity="success" message={success} onClose={() => setSuccess("")} />

      <Stack direction="row" spacing={2} sx={{ mb: 2 }}>
        <TextField
          select
          size="small"
          label="Status"
          value={statusFiltro}
          onChange={(e) => setStatusFiltro(e.target.value as NotaFiscalStatus | "")}
          sx={{ minWidth: 220 }}
        >
          {STATUS_FILTROS.map((s) => (
            <MenuItem key={s || "todos"} value={s}>
              {s ? NOTA_FISCAL_STATUS_LABEL[s] : "Todos"}
            </MenuItem>
          ))}
        </TextField>
      </Stack>

      {loading ? (
        <LoadingSpinner />
      ) : notas.length === 0 ? (
        <EmptyState message="Nenhuma nota fiscal encontrada." />
      ) : (
        <Card sx={{ border: "1px solid", borderColor: "divider" }}>
          <CardContent sx={{ p: { xs: 0, md: 1 }, "&:last-child": { pb: { xs: 0, md: 1 } } }}>
            <ResponsiveTable
              columns={COLUMNS}
              rows={notas}
              rowKey={(n) => n.id}
              renderCell={(n, col) => {
                switch (col) {
                  case 0:
                    return <Typography variant="body2" sx={{ fontFamily: "monospace" }}>{n.treinadorId.slice(0, 8)}</Typography>;
                  case 1:
                    return <Typography variant="body2">{TIPO_NOTA_FISCAL_LABEL[n.tipo]}</Typography>;
                  case 2:
                    return <Typography variant="body2">{formatarDataHora(n.dataEmissao ?? n.criadoEm)}</Typography>;
                  case 3:
                    return <Typography variant="body2">{formatarBRL(n.valor)}</Typography>;
                  case 4:
                    return (
                      <Chip
                        size="small"
                        label={NOTA_FISCAL_STATUS_LABEL[n.status]}
                        color={NOTA_FISCAL_STATUS_COLOR[n.status]}
                        aria-label={`Status: ${NOTA_FISCAL_STATUS_LABEL[n.status]}`}
                      />
                    );
                  case 5:
                    return n.motivoErro ? (
                      <Tooltip title={`${n.codigoErro ?? ""} ${n.motivoErro}`.trim()}>
                        <Typography variant="caption" color="error" sx={{ display: "block", maxWidth: 200, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                          {n.motivoErro}
                        </Typography>
                      </Tooltip>
                    ) : (
                      <Typography variant="caption" color="text.secondary">—</Typography>
                    );
                  case 6:
                    return n.status === "Erro" ? (
                      <Tooltip title="Reprocessar emissão">
                        <span>
                          <IconButton
                            size="small"
                            aria-label="Reprocessar emissão"
                            disabled={reprocessando === n.id}
                            onClick={() => reprocessar(n)}
                          >
                            <RefreshIcon fontSize="small" />
                          </IconButton>
                        </span>
                      </Tooltip>
                    ) : (
                      <Typography variant="caption" color="text.secondary">—</Typography>
                    );
                  default:
                    return null;
                }
              }}
            />
          </CardContent>
        </Card>
      )}

      {cursor && !loading && (
        <Box sx={{ display: "flex", justifyContent: "center", mt: 2 }}>
          <Button variant="outlined" onClick={carregarMais} disabled={loadingMais}>
            Carregar mais
          </Button>
        </Box>
      )}
    </Box>
  );
}
