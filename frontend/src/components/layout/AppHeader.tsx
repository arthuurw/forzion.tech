"use client";
import {
  AppBar,
  Toolbar,
  IconButton,
  Typography,
  Box,
  Avatar,
  Menu,
  MenuItem,
  Divider,
} from "@mui/material";
import MenuIcon from "@mui/icons-material/Menu";
import LogoutIcon from "@mui/icons-material/Logout";
import PersonIcon from "@mui/icons-material/Person";
import { useState } from "react";
import { useRouter } from "next/navigation";
import Logo from "@/components/ui/Logo";
import { useAuth } from "@/lib/auth/context";

interface AppHeaderProps {
  onMenuToggle?: () => void;
  showMenuButton?: boolean;
  title?: string;
}

export default function AppHeader({ onMenuToggle, showMenuButton = true, title }: AppHeaderProps) {
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
      <Toolbar>
        {showMenuButton && (
          <IconButton
            edge="start"
            onClick={onMenuToggle}
            sx={{ mr: 1, color: "white", display: { md: "none" } }}
          >
            <MenuIcon />
          </IconButton>
        )}

        <Logo size="sm" sx={{ mr: 2, "& span": { color: "white" }, "& span:first-of-type": { color: "primary.main" } }} />

        {title && (
          <Typography variant="subtitle1" sx={{ color: "white", fontWeight: 500, flex: 1 }}>
            {title}
          </Typography>
        )}

        <Box sx={{ ml: "auto" }}>
          <IconButton onClick={(e) => setAnchor(e.currentTarget)} size="small">
            <Avatar sx={{ width: 32, height: 32, bgcolor: "primary.main", color: "secondary.main", fontSize: 14, fontWeight: 700 }}>
              {user?.tipoConta?.[0] ?? "?"}
            </Avatar>
          </IconButton>
          <Menu
            anchorEl={anchor}
            open={Boolean(anchor)}
            onClose={() => setAnchor(null)}
            transformOrigin={{ horizontal: "right", vertical: "top" }}
            anchorOrigin={{ horizontal: "right", vertical: "bottom" }}
          >
            <MenuItem onClick={() => { setAnchor(null); router.push("/perfil"); }}>
              <PersonIcon fontSize="small" sx={{ mr: 1 }} /> Perfil
            </MenuItem>
            <Divider />
            <MenuItem onClick={() => { setAnchor(null); logout(); }}>
              <LogoutIcon fontSize="small" sx={{ mr: 1 }} /> Sair
            </MenuItem>
          </Menu>
        </Box>
      </Toolbar>
    </AppBar>
  );
}
