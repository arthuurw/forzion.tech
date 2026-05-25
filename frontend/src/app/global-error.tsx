"use client";
import * as Sentry from "@sentry/nextjs";
import { useEffect } from "react";

/**
 * Boundary de erro do root layout. Diferente de error.tsx (erro de segmento),
 * global-error.tsx captura falhas no proprio root layout — fora dos providers
 * (sem ThemeRegistry/MUI), entao renderiza HTML puro em pt-BR.
 */
export default function GlobalError({
  error,
}: {
  error: Error & { digest?: string };
}) {
  useEffect(() => {
    Sentry.captureException(error);
  }, [error]);

  return (
    <html lang="pt-BR">
      <body
        style={{
          minHeight: "100vh",
          margin: 0,
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          justifyContent: "center",
          gap: "1rem",
          padding: "1rem",
          textAlign: "center",
          fontFamily: "system-ui, sans-serif",
        }}
      >
        <h1 style={{ fontSize: "1.5rem", margin: 0 }}>Algo deu errado</h1>
        <p style={{ color: "#555", maxWidth: 480 }}>
          Um erro inesperado ocorreu. Recarregue a página ou tente novamente em instantes.
        </p>
        {/* Hard reload proposital: global-error roda fora do router (root
            layout falhou), entao next/link nao é confiável aqui. */}
        {/* eslint-disable-next-line @next/next/no-html-link-for-pages */}
        <a
          href="/"
          style={{
            padding: "0.6rem 1.2rem",
            background: "#1976d2",
            color: "#fff",
            borderRadius: 8,
            textDecoration: "none",
          }}
        >
          Voltar ao início
        </a>
      </body>
    </html>
  );
}
