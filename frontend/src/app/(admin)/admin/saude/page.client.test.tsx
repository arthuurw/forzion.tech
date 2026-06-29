import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import type { HealthReportConfigResponse, HealthSnapshotResponse } from "@/types";
import SaudeAdminPage from "./page";

const config: HealthReportConfigResponse = {
  id: "cfg-1",
  ativo: true,
  horaEnvioUtc: "08:30:00",
  destinatarios: ["ops@forzion.tech"],
  incluirLiveness: true,
  incluirKpis: true,
  incluirEntregabilidade: true,
  incluirErros: true,
  ultimoEnvioEm: null,
};

const snapshot: HealthSnapshotResponse = {
  id: "snap-1",
  capturadoEm: "2026-05-26T07:00:00Z",
  ambiente: "Homolog",
  statusGeral: "Degradado",
  payloadJson: "{}",
};

beforeEach(() => {
  server.use(
    http.get("*/admin/health-report/config", () => HttpResponse.json(config)),
    http.get("*/admin/health-report/snapshots", () => HttpResponse.json([snapshot])),
  );
});

describe("SaudeAdminPage", () => {
  it("carrega e exibe a configuração existente", async () => {
    render(<SaudeAdminPage />);

    expect(await screen.findByText("Relatório de saúde")).toBeInTheDocument();
    expect(screen.getByDisplayValue("ops@forzion.tech")).toBeInTheDocument();
    expect(screen.getByDisplayValue("08:30")).toBeInTheDocument();
  });

  it("mostra o status do último snapshot", async () => {
    render(<SaudeAdminPage />);

    expect(await screen.findByText("Degradado")).toBeInTheDocument();
    expect(screen.getByText("Homolog")).toBeInTheDocument();
  });

  it("salva a configuração ao clicar em Salvar (PUT recebe body correto)", async () => {
    let capturedBody: unknown = null;
    server.use(
      http.put("*/admin/health-report/config", async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json(config);
      }),
    );

    render(<SaudeAdminPage />);
    await screen.findByText("Relatório de saúde");

    fireEvent.click(screen.getByRole("button", { name: /salvar/i }));

    await waitFor(() => {
      expect(capturedBody).toMatchObject({
        ativo: true,
        horaEnvioUtc: "08:30:00",
        destinatarios: ["ops@forzion.tech"],
      });
    });
  });

  it("executa o relatório ao clicar em Enviar agora", async () => {
    const runCalled = vi.fn();
    server.use(
      http.post("*/admin/health-report/run", () => {
        runCalled();
        return HttpResponse.json(snapshot);
      }),
    );

    render(<SaudeAdminPage />);
    await screen.findByText("Relatório de saúde");

    fireEvent.click(screen.getByRole("button", { name: /enviar agora/i }));

    await waitFor(() => {
      expect(runCalled).toHaveBeenCalledTimes(1);
    });
  });
});
