"use client";
import { useState, useCallback, useEffect } from "react";
import {
  Box,
  Drawer,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Toolbar,
  BottomNavigation,
  BottomNavigationAction,
  Paper,
  Typography,
  Snackbar,
  Alert,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import { usePathname, useRouter } from "next/navigation";
import AppHeader from "./AppHeader";
import { NAV_BY_TIPO } from "./NavConfig";
import { useAuth } from "@/lib/auth/context";
import LoadingSpinner from "@/components/ui/LoadingSpinner";
import { useInactivity } from "@/hooks/useInactivity";
import { ASSINATURA_INADIMPLENTE_EVENT } from "@/lib/api/client";
import StepUpProvider from "@/components/seguranca/StepUpProvider";

const DRAWER_WIDTH = 232;
const DRAWER_COLLAPSED = 68;

export default function AppLayout({ children }: { children: React.ReactNode }) {
  const { user, isLoading, logout } = useAuth();
  const router = useRouter();
  const pathname = usePathname();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));
  const [mobileOpen, setMobileOpen] = useState(false);
  const [collapsed, setCollapsed] = useState(false);
  const [inactivityWarn, setInactivityWarn] = useState<string | null>(null);
  const [inadimplenteToast, setInadimplenteToast] = useState<string | null>(null);

  useEffect(() => {
    const handler = (e: Event) => {
      const detail = (e as CustomEvent<{ message?: string }>).detail;
      setInadimplenteToast(detail?.message ?? "Assinatura inadimplente.");
    };
    window.addEventListener(ASSINATURA_INADIMPLENTE_EVENT, handler);
    return () => window.removeEventListener(ASSINATURA_INADIMPLENTE_EVENT, handler);
  }, []);

  const handleWarn = useCallback((minutes: number) => {
    setInactivityWarn(
      `Você está sem atividade há ${minutes} minuto${minutes > 1 ? "s" : ""}. Após 20 minutos de inatividade, sua sessão será encerrada automaticamente.`
    );
  }, []);

  const handleTimeout = useCallback(() => {
    setInactivityWarn(null);
    logout();
  }, [logout]);

  useInactivity({ onWarn: handleWarn, onTimeout: handleTimeout, enabled: !!user });

  useEffect(() => {
    if (!isLoading && !user) {
      // Limpa cookies httpOnly server-side antes de redirecionar.
      // Sem isso, o middleware veria cookies válidos e redirecionaria /login → /admin em loop.
      fetch("/api/auth/logout", { method: "POST" }).finally(() => {
        router.replace("/login");
      });
    }
  }, [isLoading, user, router]);

  if (isLoading || !user) return <LoadingSpinner fullPage />;

  const navItems = NAV_BY_TIPO[user.tipoConta];
  const bottomNavItems = navItems.filter((i) => !i.drawerOnly);
  const drawerWidth = collapsed ? DRAWER_COLLAPSED : DRAWER_WIDTH;

  const drawerContent = (
    <Box sx={{ display: "flex", flexDirection: "column", height: "100%", py: 1 }}>
      <List sx={{ flex: 1, px: 1 }}>
        {navItems.map(({ label, href, Icon }) => {
          const active = pathname.startsWith(href);
          return (
            <ListItemButton
              key={href}
              selected={active}
              onClick={() => { router.push(href); setMobileOpen(false); }}
              sx={{
                mb: 0.5,
                minHeight: 46,
                borderRadius: 2,
                justifyContent: collapsed ? "center" : "flex-start",
                px: collapsed ? 1.5 : 2,
                bgcolor: active ? "primary.main" : "transparent",
                color: active ? "secondary.main" : "text.secondary",
                fontWeight: active ? 600 : 400,
                "&:hover": {
                  bgcolor: active ? "primary.dark" : "rgba(0,0,0,0.05)",
                },
                "&.Mui-selected": {
                  bgcolor: "primary.main",
                  "&:hover": { bgcolor: "primary.dark" },
                },
              }}
            >
              <ListItemIcon
                sx={{
                  minWidth: collapsed ? 0 : 36,
                  color: active ? "secondary.main" : "text.secondary",
                  mr: collapsed ? 0 : undefined,
                }}
              >
                <Icon fontSize="small" />
              </ListItemIcon>
              {!collapsed && (
                <ListItemText
                  primary={label}
                  slotProps={{ primary: { sx: { fontSize: 14, fontWeight: active ? 600 : 500 } } }}
                />
              )}
            </ListItemButton>
          );
        })}
      </List>

      {!collapsed && !isMobile && (
        <Box sx={{ px: 2, pb: 2, borderTop: "1px solid", borderColor: "divider", pt: 2 }}>
          {user?.nome && (
            <Typography variant="body2" noWrap sx={{ fontWeight: 600 }} title={user.nome}>
              {user.nome}
            </Typography>
          )}
          <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 500 }}>
            {user?.tipoConta}
          </Typography>
        </Box>
      )}
    </Box>
  );

  return (
    <Box sx={{ display: "flex", minHeight: "100dvh", bgcolor: "background.default" }}>
      <AppHeader
        onMenuToggle={() => (isMobile ? setMobileOpen((v) => !v) : setCollapsed((v) => !v))}
      />
      <StepUpProvider />

      {!isMobile && (
        <Drawer
          variant="permanent"
          sx={{
            width: drawerWidth,
            flexShrink: 0,
            transition: "width 0.2s ease",
            "& .MuiDrawer-paper": {
              width: drawerWidth,
              transition: "width 0.2s ease",
              overflowX: "hidden",
              borderRight: "1px solid",
              borderColor: "divider",
              bgcolor: "background.paper",
            },
          }}
        >
          <Toolbar sx={{ minHeight: { xs: 60, sm: 64 } }} />
          {drawerContent}
        </Drawer>
      )}

      {isMobile && (
        <Drawer
          variant="temporary"
          open={mobileOpen}
          onClose={() => setMobileOpen(false)}
          ModalProps={{ keepMounted: true }}
          sx={{
            "& .MuiDrawer-paper": { width: DRAWER_WIDTH, bgcolor: "background.paper" },
          }}
        >
          <Toolbar sx={{ minHeight: 60 }} />
          {drawerContent}
        </Drawer>
      )}

      <Box
        component="main"
        sx={{
          flex: 1,
          p: { xs: 2.5, md: 3.5 },
          pb: { xs: "calc(72px + env(safe-area-inset-bottom, 0px))", md: 3.5 },
          mt: { xs: "60px", sm: "64px" },
          minWidth: 0,
          maxWidth: "100%",
        }}
      >
        {children}
      </Box>

      {isMobile && (
        <Paper
          elevation={0}
          sx={{
            position: "fixed",
            bottom: 0,
            left: 0,
            right: 0,
            zIndex: "appBar",
            borderTop: "1px solid",
            borderColor: "divider",
            pb: "env(safe-area-inset-bottom, 0px)",
          }}
        >
          <BottomNavigation
            value={bottomNavItems.findIndex(({ href }) => pathname.startsWith(href))}
            onChange={(_, i) => router.push(bottomNavItems[i].href)}
            showLabels={bottomNavItems.length <= 4}
            sx={{ "& .Mui-selected": { color: "secondary.main" } }}
          >
            {bottomNavItems.map(({ label, Icon }) => (
              <BottomNavigationAction key={label} label={label} icon={<Icon />} aria-label={label} />
            ))}
          </BottomNavigation>
        </Paper>
      )}

      <Snackbar
        open={!!inactivityWarn}
        anchorOrigin={{ vertical: "bottom", horizontal: "center" }}
        sx={{ mb: isMobile ? 8 : 2 }}
      >
        <Alert
          severity="warning"
          variant="filled"
          onClose={() => setInactivityWarn(null)}
          sx={{ maxWidth: 480 }}
        >
          {inactivityWarn}
        </Alert>
      </Snackbar>

      <Snackbar
        open={!!inadimplenteToast}
        autoHideDuration={8000}
        onClose={() => setInadimplenteToast(null)}
        anchorOrigin={{ vertical: "top", horizontal: "center" }}
      >
        <Alert
          severity="error"
          variant="filled"
          onClose={() => setInadimplenteToast(null)}
          sx={{ maxWidth: 480 }}
        >
          {inadimplenteToast}
        </Alert>
      </Snackbar>
    </Box>
  );
}
