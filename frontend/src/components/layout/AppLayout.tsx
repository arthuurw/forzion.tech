"use client";
import { useState } from "react";
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
  useMediaQuery,
  useTheme,
} from "@mui/material";
import { usePathname, useRouter } from "next/navigation";
import AppHeader from "./AppHeader";
import { NAV_BY_TIPO } from "./NavConfig";
import { useAuth } from "@/lib/auth/context";
import LoadingSpinner from "@/components/ui/LoadingSpinner";

const DRAWER_WIDTH = 220;
const DRAWER_COLLAPSED = 64;

export default function AppLayout({ children }: { children: React.ReactNode }) {
  const { user, isLoading } = useAuth();
  const router = useRouter();
  const pathname = usePathname();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));
  const [mobileOpen, setMobileOpen] = useState(false);
  const [collapsed, setCollapsed] = useState(false);

  if (isLoading) return <LoadingSpinner fullPage />;

  const navItems = user ? NAV_BY_TIPO[user.tipoConta] : [];
  const drawerWidth = collapsed ? DRAWER_COLLAPSED : DRAWER_WIDTH;

  const drawerContent = (
    <List sx={{ pt: 1 }}>
      {navItems.map(({ label, href, Icon }) => {
        const active = pathname.startsWith(href);
        return (
          <ListItemButton
            key={href}
            selected={active}
            onClick={() => { router.push(href); setMobileOpen(false); }}
            sx={{
              mx: 1,
              borderRadius: 2,
              mb: 0.5,
              minHeight: 44,
              justifyContent: collapsed ? "center" : "flex-start",
              "& .MuiListItemText-root": { opacity: collapsed ? 0 : 1 },
            }}
          >
            <ListItemIcon sx={{ minWidth: collapsed ? 0 : 36, color: active ? "primary.main" : "inherit" }}>
              <Icon fontSize="small" />
            </ListItemIcon>
            {!collapsed && <ListItemText primary={label} />}
          </ListItemButton>
        );
      })}
    </List>
  );

  return (
    <Box sx={{ display: "flex", minHeight: "100vh" }}>
      <AppHeader
        onMenuToggle={() => (isMobile ? setMobileOpen((v) => !v) : setCollapsed((v) => !v))}
      />

      {/* Sidebar desktop */}
      {!isMobile && (
        <Drawer
          variant="permanent"
          sx={{
            width: drawerWidth,
            flexShrink: 0,
            transition: "width 0.2s",
            "& .MuiDrawer-paper": {
              width: drawerWidth,
              transition: "width 0.2s",
              overflowX: "hidden",
              borderRight: "1px solid",
              borderColor: "divider",
            },
          }}
        >
          <Toolbar />
          {drawerContent}
        </Drawer>
      )}

      {/* Drawer mobile */}
      {isMobile && (
        <Drawer
          variant="temporary"
          open={mobileOpen}
          onClose={() => setMobileOpen(false)}
          ModalProps={{ keepMounted: true }}
          sx={{ "& .MuiDrawer-paper": { width: DRAWER_WIDTH } }}
        >
          <Toolbar />
          {drawerContent}
        </Drawer>
      )}

      {/* Conteúdo principal */}
      <Box
        component="main"
        sx={{
          flex: 1,
          p: { xs: 2, md: 3 },
          pb: { xs: 9, md: 3 },
          mt: "64px",
          minWidth: 0,
        }}
      >
        {children}
      </Box>

      {/* Bottom nav mobile */}
      {isMobile && (
        <Paper
          elevation={3}
          sx={{ position: "fixed", bottom: 0, left: 0, right: 0, zIndex: "appBar" }}
        >
          <BottomNavigation
            value={navItems.findIndex(({ href }) => pathname.startsWith(href))}
            onChange={(_, i) => router.push(navItems[i].href)}
            showLabels
          >
            {navItems.map(({ label, Icon }) => (
              <BottomNavigationAction key={label} label={label} icon={<Icon />} />
            ))}
          </BottomNavigation>
        </Paper>
      )}
    </Box>
  );
}
