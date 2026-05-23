"use client";
import { useReportWebVitals } from "next/web-vitals";
import * as Sentry from "@sentry/nextjs";

/**
 * RUM de Web Vitals (LCP, CLS, INP, FCP, TTFB). Encaminha cada métrica ao
 * Sentry como breadcrumb — fica anexada a qualquer erro/replay capturado na
 * mesma sessão, dando contexto de performance.
 *
 * Os core vitals de pageload também são coletados automaticamente pelo
 * browserTracingIntegration do Sentry. A agregação p75 / dashboards entra na
 * Fase 18 (observability).
 *
 * Componente sem UI; montado uma vez no root layout.
 */
export function WebVitals() {
  useReportWebVitals((metric) => {
    Sentry.addBreadcrumb({
      category: "web-vitals",
      level: "info",
      message: metric.name,
      data: {
        value: metric.value,
        id: metric.id,
        label: metric.label,
      },
    });
  });

  return null;
}
