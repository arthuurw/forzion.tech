"use client";
import { useEffect, useState } from "react";
import {
  IconButton,
  Badge,
  Menu,
  Box,
  Typography,
  ListItemButton,
  Divider,
} from "@mui/material";
import NotificationsIcon from "@mui/icons-material/Notifications";
import EmptyState from "@/components/ui/EmptyState";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import AlertBanner from "@/components/ui/AlertBanner";
import { notificacoesApi } from "@/lib/api/notificacoes";
import { extractApiError } from "@/lib/api/extractApiError";
import { formatarDataHora } from "@/lib/utils/formatting";
import type { NotificacaoResponse } from "@/types";

export default function NotificacoesBell() {
  const [anchor, setAnchor] = useState<null | HTMLElement>(null);
  const [naoLidas, setNaoLidas] = useState(0);
  const [itens, setItens] = useState<NotificacaoResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    notificacoesApi
      .contarNaoLidas()
      .then((res) => setNaoLidas(res.data.total))
      .catch(() => {});
  }, []);

  const abrir = async (el: HTMLElement) => {
    setAnchor(el);
    setLoading(true);
    setError("");
    try {
      const res = await notificacoesApi.listar();
      setItens(res.data);
    } catch (err) {
      setError(extractApiError(err, "Erro ao carregar notificações."));
    } finally {
      setLoading(false);
    }
  };

  const marcarLida = async (n: NotificacaoResponse) => {
    if (n.lida) return;
    try {
      await notificacoesApi.marcarLida(n.id);
      setItens((atual) => atual.map((i) => (i.id === n.id ? { ...i, lida: true } : i)));
      setNaoLidas((c) => Math.max(0, c - 1));
    } catch (err) {
      setError(extractApiError(err, "Erro ao marcar como lida."));
    }
  };

  return (
    <>
      <IconButton
        aria-label={naoLidas > 0 ? `Notificações, ${naoLidas} não lidas` : "Notificações"}
        aria-haspopup="true"
        aria-expanded={Boolean(anchor)}
        onClick={(e) => abrir(e.currentTarget)}
        sx={{ color: "rgba(255,255,255,0.8)" }}
      >
        <Badge badgeContent={naoLidas} color="primary" overlap="circular" max={99}>
          <NotificationsIcon />
        </Badge>
      </IconButton>

      <Menu
        anchorEl={anchor}
        open={Boolean(anchor)}
        onClose={() => setAnchor(null)}
        transformOrigin={{ horizontal: "right", vertical: "top" }}
        anchorOrigin={{ horizontal: "right", vertical: "bottom" }}
        slotProps={{
          paper: {
            sx: {
              mt: 0.5,
              width: { xs: "calc(100vw - 32px)", sm: 380 },
              maxWidth: "100vw",
              maxHeight: "70vh",
              borderRadius: 2,
            },
          },
        }}
      >
        <Box sx={{ px: 2, py: 1.5 }}>
          <Typography variant="subtitle1" component="h2">Notificações</Typography>
        </Box>
        <Divider />

        {error && (
          <Box sx={{ px: 2, pt: 1.5 }}>
            <AlertBanner open message={error} onClose={() => setError("")} />
          </Box>
        )}

        {loading ? (
          <LoadingSpinner />
        ) : itens.length === 0 && !error ? (
          <EmptyState message="Nenhuma notificação por aqui." />
        ) : (
          itens.map((n) => (
            <ListItemButton
              key={n.id}
              onClick={() => marcarLida(n)}
              sx={{ display: "block", py: 1.5, minHeight: 44, bgcolor: n.lida ? "transparent" : "action.subtleBg" }}
            >
              <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                {!n.lida && (
                  <Box aria-hidden sx={{ width: 8, height: 8, borderRadius: "50%", bgcolor: "primary.main", flexShrink: 0 }} />
                )}
                <Typography variant="body2" sx={{ fontWeight: n.lida ? 500 : 700, overflowWrap: "anywhere" }}>
                  {n.titulo}
                </Typography>
              </Box>
              <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25, overflowWrap: "anywhere", whiteSpace: "normal" }}>
                {n.corpo}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                {formatarDataHora(n.createdAt)}
              </Typography>
            </ListItemButton>
          ))
        )}
      </Menu>
    </>
  );
}
