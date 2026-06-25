import type { Metadata, Viewport } from "next";
import { Inter } from "next/font/google";
import { AppRouterCacheProvider } from "@mui/material-nextjs/v16-appRouter";
import { AuthProvider } from "@/lib/auth/context";
import { QueryProvider } from "@/lib/query/QueryProvider";
import { ErrorBoundary } from "@/components/ui/ErrorBoundary";
import ThemeRegistry from "@/lib/theme/ThemeRegistry";
import { WebVitals } from "@/components/observability/WebVitals";
import { ReplayManager } from "@/components/observability/ReplayManager";
import ConsentProvider from "@/components/ui/ConsentProvider";
import "@/styles/globals.css";

const inter = Inter({
  weight: ["400", "500", "600", "700"],
  subsets: ["latin"],
  display: "swap",
});

const SITE_URL = process.env.NEXT_PUBLIC_SITE_URL ?? "https://forzion.tech";
const SITE_DESCRIPTION = "Plataforma de gestão de treinos para personal trainers";

export const metadata: Metadata = {
  metadataBase: new URL(SITE_URL),
  title: {
    default: "forzion.tech — Gestão para Personal Trainers",
    template: "%s | forzion.tech",
  },
  description: SITE_DESCRIPTION,
  openGraph: {
    type: "website",
    siteName: "forzion.tech",
    locale: "pt_BR",
    url: "/",
    title: "forzion.tech — Gestão para Personal Trainers",
    description: SITE_DESCRIPTION,
  },
  twitter: {
    card: "summary_large_image",
  },
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
        <a href="#main-content" className="skip-link">
          Pular para o conteúdo
        </a>
        <WebVitals />
        <AppRouterCacheProvider>
          <ThemeRegistry>
            <ErrorBoundary>
              <AuthProvider>
                <ConsentProvider />
                <ReplayManager />
                <QueryProvider>{children}</QueryProvider>
              </AuthProvider>
            </ErrorBoundary>
          </ThemeRegistry>
        </AppRouterCacheProvider>
      </body>
    </html>
  );
}
