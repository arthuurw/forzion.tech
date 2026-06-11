/**
 * renderWithProviders — wrapper de render do React Testing Library
 * que aplica os providers globais da aplicacao (Theme + Localization + Auth + Snackbar).
 *
 * Uso:
 *   import { renderWithProviders } from "@/test/render";
 *   import { buildSessionUser } from "@/test/factories";
 *
 *   renderWithProviders(<MeuComponente />);
 *   // skipAuth quando o teste mocka useAuth manualmente com um SessionUser:
 *   vi.mocked(useAuth).mockReturnValue({ user: buildSessionUser(), ... });
 *   renderWithProviders(<MeuComponente />, { skipAuth: true });
 */
import { render, type RenderOptions, type RenderResult } from "@testing-library/react";
import type { ReactElement, ReactNode } from "react";
import { ThemeProvider, CssBaseline } from "@mui/material";
import { LocalizationProvider } from "@mui/x-date-pickers/LocalizationProvider";
import { AdapterDayjs } from "@mui/x-date-pickers/AdapterDayjs";
import "dayjs/locale/pt-br";
import theme from "@/lib/theme";
import { AuthProvider } from "@/lib/auth/context";
import { SnackbarProvider } from "@/components/ui/SnackbarProvider";

export interface ProvidersOptions {
  /**
   * Pular AuthProvider (util quando teste mocka contexto manualmente).
   */
  skipAuth?: boolean;
  /**
   * Pular SnackbarProvider (util para tests rapidos sem UI feedback).
   */
  skipSnackbar?: boolean;
}

interface AllProvidersProps extends ProvidersOptions {
  children: ReactNode;
}

function AllProviders({ children, skipAuth, skipSnackbar }: AllProvidersProps) {
  let tree: ReactNode = children;

  if (!skipSnackbar) {
    tree = <SnackbarProvider>{tree}</SnackbarProvider>;
  }
  if (!skipAuth) {
    tree = <AuthProvider>{tree}</AuthProvider>;
  }

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <LocalizationProvider dateAdapter={AdapterDayjs} adapterLocale="pt-br">
        {tree}
      </LocalizationProvider>
    </ThemeProvider>
  );
}

export function renderWithProviders(
  ui: ReactElement,
  options: Omit<RenderOptions, "wrapper"> & ProvidersOptions = {},
): RenderResult {
  const { skipAuth, skipSnackbar, ...renderOptions } = options;
  return render(ui, {
    wrapper: ({ children }) => (
      <AllProviders skipAuth={skipAuth} skipSnackbar={skipSnackbar}>
        {children}
      </AllProviders>
    ),
    ...renderOptions,
  });
}

export * from "@testing-library/react";
