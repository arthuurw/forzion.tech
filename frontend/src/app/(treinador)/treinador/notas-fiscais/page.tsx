"use client";
import { useCallback, useState } from "react";
import Link from "next/link";
import { Box, Typography, Card, CardContent, Chip, Button, IconButton, Tooltip } from "@mui/material";
import DownloadIcon from "@mui/icons-material/Download";
import ReceiptLongIcon from "@mui/icons-material/ReceiptLong";
import SettingsIcon from "@mui/icons-material/Settings";
import AlertBanner from "@/components/ui/AlertBanner";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import EmptyState from "@/components/ui/EmptyState";
import { ResponsiveTable, type Column } from "@/components/ui/ResponsiveTable";
import {
  nfseApi, type NotaFiscalResumo,
  NOTA_FISCAL_STATUS_LABEL, NOTA_FISCAL_STATUS_COLOR, TIPO_NOTA_FISCAL_LABEL,
} from "@/lib/api/nfse";
import { extractApiError } from "@/lib/api/extractApiError";
import { formatarBRL, formatarDataHora } from "@/lib/utils/formatting";
import { useCursorList } from "@/hooks/useCursorList";

const COLUMNS: Column[] = [
  { label: "Tipo" },
  { label: "Emissão" },
  { label: "Valor", align: "right" },
  { label: "Status" },
  { label: "DANFSe", align: "right" },
];

export default function NotasFiscaisTreinadorPage() {
  const [baixando, setBaixando] = useState<string | null>(null);

  const fetcher = useCallback(
    (_filtro: undefined, aposId: string | undefined, signal: AbortSignal) =>
      nfseApi.listNotasTreinador(aposId ? { aposId } : undefined, signal),
    [],
  );

  const {
    itens: notas, cursor, loading, loadingMais, error, setError, carregarMais,
  } = useCursorList<NotaFiscalResumo, undefined>({
    fetcher,
    filtro: undefined,
    errorMessage: "Erro ao carregar notas fiscais.",
  });

  const baixarDanfse = async (nota: NotaFiscalResumo) => {
    setError("");
    setBaixando(nota.id);
    try {
      const res = await nfseApi.getDanfse(nota.id);
      window.open(res.data.danfseRef, "_blank", "noopener");
    } catch (err) {
      setError(extractApiError(err, "DANFSe indisponível para esta nota."));
    } finally {
      setBaixando(null);
    }
  };

  if (loading) return <LoadingSpinner />;

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 1.5, mb: 3 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
          <ReceiptLongIcon sx={{ color: "text.secondary" }} />
          <Typography variant="h5" sx={{ fontWeight: 700 }}>Notas fiscais</Typography>
        </Box>
        <Button
          component={Link}
          href="/treinador/dados-fiscais"
          variant="outlined"
          size="small"
          startIcon={<SettingsIcon />}
        >
          Dados fiscais
        </Button>
      </Box>

      <AlertBanner open={!!error} message={error} onClose={() => setError("")} />

      {notas.length === 0 ? (
        <EmptyState message="Nenhuma nota fiscal emitida ainda." />
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
                    return <Typography variant="body2">{TIPO_NOTA_FISCAL_LABEL[n.tipo]}</Typography>;
                  case 1:
                    return <Typography variant="body2">{formatarDataHora(n.dataEmissao ?? n.criadoEm)}</Typography>;
                  case 2:
                    return <Typography variant="body2">{formatarBRL(n.valor)}</Typography>;
                  case 3:
                    return (
                      <Chip
                        size="small"
                        label={NOTA_FISCAL_STATUS_LABEL[n.status]}
                        color={NOTA_FISCAL_STATUS_COLOR[n.status]}
                        aria-label={`Status: ${NOTA_FISCAL_STATUS_LABEL[n.status]}`}
                      />
                    );
                  case 4:
                    return n.temDanfse ? (
                      <Tooltip title="Baixar DANFSe">
                        <span>
                          <IconButton
                            size="small"
                            aria-label="Baixar DANFSe"
                            disabled={baixando === n.id}
                            onClick={() => baixarDanfse(n)}
                          >
                            <DownloadIcon fontSize="small" />
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

      {cursor && (
        <Box sx={{ display: "flex", justifyContent: "center", mt: 2 }}>
          <Button variant="outlined" onClick={carregarMais} disabled={loadingMais}>
            Carregar mais
          </Button>
        </Box>
      )}
    </Box>
  );
}
