import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import type { HealthReportConfigResponse, HealthSnapshotResponse } from "@/types";

vi.mock("@/lib/api/admin", () => ({
  adminApi: {
    getHealthReportConfig: vi.fn(),
    updateHealthReportConfig: vi.fn(),
    listHealthSnapshots: vi.fn(),
    runHealthReport: vi.fn(),
  },
}));

import { adminApi } from "@/lib/api/admin";
import SaudeAdminPage from "./page";

const mockApi = vi.mocked(adminApi);

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
  vi.clearAllMocks();
  mockApi.getHealthReportConfig.mockResolvedValue({ status: 200, data: config } as never);
  mockApi.listHealthSnapshots.mockResolvedValue({ data: [snapshot] } as never);
  mockApi.updateHealthReportConfig.mockResolvedValue({ data: config } as never);
  mockApi.runHealthReport.mockResolvedValue({ data: snapshot } as never);
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

  it("salva a configuração ao clicar em Salvar", async () => {
    render(<SaudeAdminPage />);
    await screen.findByText("Relatório de saúde");

    fireEvent.click(screen.getByRole("button", { name: /salvar/i }));

    await waitFor(() => {
      expect(mockApi.updateHealthReportConfig).toHaveBeenCalledTimes(1);
    });
    expect(mockApi.updateHealthReportConfig).toHaveBeenCalledWith(
      expect.objectContaining({
        ativo: true,
        horaEnvioUtc: "08:30:00",
        destinatarios: ["ops@forzion.tech"],
      }),
    );
  });

  it("executa o relatório ao clicar em Enviar agora", async () => {
    render(<SaudeAdminPage />);
    await screen.findByText("Relatório de saúde");

    fireEvent.click(screen.getByRole("button", { name: /enviar agora/i }));

    await waitFor(() => {
      expect(mockApi.runHealthReport).toHaveBeenCalledTimes(1);
    });
  });
});
