"use client";
import { createTheme, alpha } from "@mui/material/styles";
import { ptBR } from "@mui/material/locale";

declare module "@mui/material/styles" {
  interface Palette {
    brand: { label: string };
  }
  interface PaletteOptions {
    brand?: { label?: string };
  }
  interface TypeAction {
    subtleBg: string;
  }
}

const theme = createTheme(
  {
    palette: {
      primary: {
        main: "#F5C400",
        light: "#FFD740",
        dark: "#C9A000",
        contrastText: "#1A1A1A",
      },
      secondary: {
        main: "#1A1A1A",
        light: "#2E2E2E",
        contrastText: "#FFFFFF",
      },
      error: {
        // #C62828: 5.62:1 on #FFF / 5.29:1 on #F7F8FA (text/icon on light, WCAG AA pass);
        // white-on-error in contained buttons = 5.62:1 (also AA). Prev #D32F2F was only 4.98/4.69 (thin margin).
        main: "#C62828",
      },
      background: {
        default: "#F7F8FA",
        paper: "#FFFFFF",
      },
      text: {
        // #111827: 17.74:1 on #FFF / 16.69:1 on #F7F8FA (AA pass).
        primary: "#111827",
        // #4B5563 (gray-600): 7.56:1 on #FFF / 7.11:1 on #F7F8FA — WCAG AA pass on both app
        // backgrounds with margin. Prev #6B7280 (gray-500) was 4.83/4.55 (passed white but razor-thin
        // on #F7F8FA, and MUI derives placeholder/disabled/helperText/secondary from it). F18 fix.
        secondary: "#4B5563",
      },
      divider: "rgba(0,0,0,0.08)",
      action: { subtleBg: alpha("#1A1A1A", 0.06) },
      // Overline accent for landing section labels; darker than primary for AA contrast on light bg.
      brand: { label: "#7a6300" },
    },
    typography: {
      fontFamily: "'Inter', 'Roboto', sans-serif",
      h1: { fontWeight: 700, letterSpacing: "-0.02em" },
      h2: { fontWeight: 700, letterSpacing: "-0.015em" },
      h3: { fontWeight: 700, letterSpacing: "-0.01em" },
      h4: { fontWeight: 700, letterSpacing: "-0.01em" },
      h5: { fontWeight: 600, letterSpacing: "-0.005em" },
      h6: { fontWeight: 600 },
      subtitle1: { fontWeight: 500 },
      button: { textTransform: "none", fontWeight: 600, letterSpacing: 0 },
    },
    shape: {
      borderRadius: 12,
    },
    shadows: [
      "none",
      "0 1px 2px rgba(0,0,0,0.06), 0 1px 3px rgba(0,0,0,0.08)",
      "0 2px 4px rgba(0,0,0,0.06), 0 1px 6px rgba(0,0,0,0.08)",
      "0 4px 8px rgba(0,0,0,0.06), 0 2px 8px rgba(0,0,0,0.08)",
      "0 4px 16px rgba(0,0,0,0.08), 0 2px 6px rgba(0,0,0,0.06)",
      "0 8px 24px rgba(0,0,0,0.1), 0 2px 8px rgba(0,0,0,0.06)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
      "0 12px 32px rgba(0,0,0,0.12)",
    ],
    components: {
      MuiButton: {
        defaultProps: { disableElevation: true },
        styleOverrides: {
          root: {
            borderRadius: 10,
            paddingTop: 10,
            paddingBottom: 10,
            transition: "all 0.15s ease",
            "&.MuiButton-containedPrimary:hover": { backgroundColor: "#E6B800" },
            "&.MuiButton-outlinedSecondary": {
              borderColor: alpha("#1A1A1A", 0.3),
              "&:hover": { borderColor: "#1A1A1A", backgroundColor: alpha("#1A1A1A", 0.04) },
            },
          },
          sizeLarge: {
            paddingTop: 13,
            paddingBottom: 13,
            paddingLeft: 28,
            paddingRight: 28,
            fontSize: "1rem",
          },
        },
      },
      MuiTextField: {
        defaultProps: { variant: "outlined", size: "small", fullWidth: true },
      },
      MuiOutlinedInput: {
        styleOverrides: {
          root: {
            borderRadius: 10,
            backgroundColor: "#fff",
            "& fieldset": { borderColor: "rgba(0,0,0,0.15)" },
            "&:hover fieldset": { borderColor: "rgba(0,0,0,0.4)" },
          },
          input: {
            // Prevents iOS Safari from auto-zooming on focus (requires >= 16px)
            fontSize: "1rem",
            "@media (min-width: 600px)": {
              fontSize: "0.875rem",
            },
          },
        },
      },
      MuiCard: {
        styleOverrides: {
          root: {
            borderRadius: 16,
            boxShadow: "0 1px 3px rgba(0,0,0,0.06), 0 1px 8px rgba(0,0,0,0.06)",
          },
        },
      },
      MuiPaper: {
        styleOverrides: {
          rounded: { borderRadius: 16 },
        },
      },
      MuiChip: {
        styleOverrides: {
          root: { fontWeight: 600, borderRadius: 8 },
        },
      },
      MuiListItemButton: {
        styleOverrides: {
          root: {
            borderRadius: 10,
            transition: "all 0.15s ease",
          },
        },
      },
      MuiAppBar: {
        styleOverrides: {
          root: { backgroundImage: "none" },
        },
      },
      MuiDrawer: {
        styleOverrides: {
          paper: { backgroundImage: "none" },
        },
      },
      MuiDivider: {
        styleOverrides: {
          root: { borderColor: "rgba(0,0,0,0.07)" },
        },
      },
    },
  },
  ptBR
);

export default theme;
