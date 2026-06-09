"use client";
import {
  AppBar,
  Toolbar,
  IconButton,
  Box,
  Avatar,
  Menu,
  MenuItem,
  Divider,
  Typography,
} from "@mui/material";
import MenuIcon from "@mui/icons-material/Menu";
import LogoutIcon from "@mui/icons-material/Logout";
import PersonIcon from "@mui/icons-material/Person";
import { useState } from "react";
import { useRouter } from "next/navigation";
import Logo from "@/components/ui/Logo";
import { useAuth, homeRouteFor } from "@/lib/auth/context";
import Link from "next/link";

interface AppHeaderProps {
  onMenuToggle?: () => void;
  showMenuButton?: boolean;
}

const TIPO_LABEL: Record<string, string> = {
  SystemAdmin: "Admin",
  Treinador: "Treinador",
  Aluno: "Aluno",
};

export default function AppHeader({ onMenuToggle, showMenuButton = true }: AppHeaderProps) {
  const { user, logout } = useAuth();
  const router = useRouter();
  const [anchor, setAnchor] = useState<null | HTMLElement>(null);

  return (
    <AppBar
      position="fixed"
      elevation={0}
      sx={{
        bgcolor: "secondary.main",
        borderBottom: "2px solid",
        borderColor: "primary.main",
        zIndex: (t) => t.zIndex.drawer + 1,
      }}
    >
      <Toolbar sx={{ minHeight: { xs: 60, sm: 64 } }}>
        {showMenuButton && (
          <IconButton
            edge="start"
            onClick={onMenuToggle}
            aria-label="Abrir menu"
            sx={{ mr: 1.5, color: "rgba(255,255,255,0.8)" }}
          >
            <MenuIcon />
          </IconButton>
        )}

        <Link href={user ? homeRouteFor(user.tipoConta) : "/"} style={{ textDecoration: "none" }}>
          <Logo
            size="sm"
            sx={{ "& span": { color: "rgba(255,255,255,0.9)" }, "& span:first-of-type": { color: "primary.main" } }}
          />
        </Link>

        <Box sx={{ ml: "auto", display: "flex", alignItems: "center", gap: 1 }}>
          <Box
            component="button"
            onClick={(e) => setAnchor(e.currentTarget as HTMLElement)}
            sx={{
              display: "flex",
              alignItems: "center",
              gap: 1.5,
              px: 1.5,
              py: 0.75,
              borderRadius: 2,
              border: "none",
              bgcolor: "rgba(255,255,255,0.08)",
              cursor: "pointer",
              transition: "background 0.15s",
              "&:hover": { bgcolor: "rgba(255,255,255,0.14)" },
            }}
          >
            <Avatar
              sx={{
                width: 30,
                height: 30,
                bgcolor: "primary.main",
                color: "secondary.main",
                fontSize: 13,
                fontWeight: 700,
              }}
            >
              {user?.nome?.trim()?.[0]?.toUpperCase() ?? user?.tipoConta?.[0] ?? "?"}
            </Avatar>
            <Box sx={{ display: { xs: "none", sm: "flex" }, flexDirection: "column", alignItems: "flex-start", lineHeight: 1.1 }}>
              <Typography variant="body2" sx={{ color: "rgba(255,255,255,0.95)", fontWeight: 600, lineHeight: 1.2 }}>
                {user?.nome || (TIPO_LABEL[user?.tipoConta ?? ""] ?? user?.tipoConta)}
              </Typography>
              <Typography variant="caption" sx={{ color: "rgba(255,255,255,0.6)", lineHeight: 1.2 }}>
                {TIPO_LABEL[user?.tipoConta ?? ""] ?? user?.tipoConta}
              </Typography>
            </Box>
          </Box>

          <Menu
            anchorEl={anchor}
            open={Boolean(anchor)}
            onClose={() => setAnchor(null)}
            transformOrigin={{ horizontal: "right", vertical: "top" }}
            anchorOrigin={{ horizontal: "right", vertical: "bottom" }}
            slotProps={{
              paper: { sx: { mt: 0.5, minWidth: 180, borderRadius: 2, boxShadow: "0 8px 24px rgba(0,0,0,0.12)" } },
            }}
          >
            <MenuItem onClick={() => { setAnchor(null); router.push("/perfil"); }} sx={{ gap: 1.5, py: 1.5 }}>
              <PersonIcon fontSize="small" sx={{ color: "text.secondary" }} /> Meu Perfil
            </MenuItem>
            <Divider />
            <MenuItem onClick={() => { setAnchor(null); logout(); }} sx={{ gap: 1.5, py: 1.5, color: "error.main" }}>
              <LogoutIcon fontSize="small" /> Sair
            </MenuItem>
          </Menu>
        </Box>
      </Toolbar>
    </AppBar>
  );
}
