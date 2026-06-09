import { describe, it, expect, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type { HealthReportConfigResponse } from "@/types";

const config: HealthReportConfigResponse = {
  id: "cfg-1", ativo: false, horaEnvioUtc: "07:00:00", destinatarios: [],
  incluirLiveness: true, incluirKpis: true, incluirEntregabilidade: true,
  incluirErros: true, ultimoEnvioEm: null,
};

describe("SaudeAdminPage — snapshots", () => {
  beforeEach(() => {
    server.use(
      http.get("*/admin/health-report/config", () => HttpResponse.json(config)),
    );
  });

  it("snapshots falha → exibe placeholder 'Snapshots indisponíveis'", async () => {
    server.use(
      http.get("*/admin/health-report/snapshots", () => HttpResponse.json({ title: "boom" }, { status: 500 })),
    );
    const { default: Page } = await import("@/app/(admin)/admin/saude/page");
    render(<Page />);
    expect(await screen.findByText("Snapshots indisponíveis no momento.")).toBeInTheDocument();
  });

  it("snapshots vazio → exibe 'Nenhum snapshot ainda'", async () => {
    server.use(
      http.get("*/admin/health-report/snapshots", () => HttpResponse.json([])),
    );
    const { default: Page } = await import("@/app/(admin)/admin/saude/page");
    render(<Page />);
    expect(await screen.findByText("Nenhum snapshot ainda.")).toBeInTheDocument();
  });
});
