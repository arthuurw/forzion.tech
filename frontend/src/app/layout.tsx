import type { Metadata, Viewport } from "next";
import { Inter } from "next/font/google";
import { AppRouterCacheProvider } from "@mui/material-nextjs/v16-appRouter";
import { AuthProvider } from "@/lib/auth/context";
import { SnackbarProvider } from "@/components/ui/SnackbarProvider";
import { ErrorBoundary } from "@/components/ui/ErrorBoundary";
import ThemeRegistry from "@/lib/theme/ThemeRegistry";
import { WebVitals } from "@/components/observability/WebVitals";
import "@/styles/globals.css";

const inter = Inter({
  weight: ["400", "500", "600", "700"],
  subsets: ["latin"],
  display: "swap",
});

export const metadata: Metadata = {
  title: "forzion.tech",
  description: "Plataforma de gestão de treinos para personal trainers",
};

export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
  viewportFit: "cover",
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="pt-BR" className={inter.className} suppressHydrationWarning>
      <body>
        <WebVitals />
        <AppRouterCacheProvider>
          <ThemeRegistry>
            <ErrorBoundary>
              <AuthProvider>
                <SnackbarProvider>{children}</SnackbarProvider>
              </AuthProvider>
            </ErrorBoundary>
          </ThemeRegistry>
        </AppRouterCacheProvider>
      </body>
    </html>
  );
}
