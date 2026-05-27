import { describe, it, expect, vi, beforeEach } from "vitest";
import type { AtualizarHealthReportConfigRequest } from "@/types";

vi.mock("./client", () => ({
  apiClient: { get: vi.fn(), put: vi.fn(), post: vi.fn() },
}));

import { apiClient } from "./client";
import { adminApi } from "./admin";

const mock = vi.mocked(apiClient);

beforeEach(() => vi.clearAllMocks());

describe("adminApi — relatório de saúde", () => {
  it("getHealthReportConfig usa GET no endpoint de config", () => {
    adminApi.getHealthReportConfig();
    expect(mock.get).toHaveBeenCalledWith("/admin/health-report/config");
  });

  it("updateHealthReportConfig usa PUT com o corpo", () => {
    const data: AtualizarHealthReportConfigRequest = {
      ativo: true,
      horaEnvioUtc: "07:00:00",
      destinatarios: ["a@b.com"],
      incluirLiveness: true,
      incluirKpis: true,
      incluirEntregabilidade: true,
      incluirErros: true,
    };
    adminApi.updateHealthReportConfig(data);
    expect(mock.put).toHaveBeenCalledWith("/admin/health-report/config", data);
  });

  it("listHealthSnapshots usa GET com params", () => {
    adminApi.listHealthSnapshots({ limite: 5 });
    expect(mock.get).toHaveBeenCalledWith("/admin/health-report/snapshots", { params: { limite: 5 } });
  });

  it("runHealthReport usa POST no endpoint de run", () => {
    adminApi.runHealthReport();
    expect(mock.post).toHaveBeenCalledWith("/admin/health-report/run");
  });
});
