import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor, fireEvent } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";
import { renderWithProviders } from "@/test/render";
import type { HealthReportConfigResponse } from "@/types";

vi.mock("next/navigation", () => ({
  useRouter: vi.fn(() => ({ push: vi.fn(), back: vi.fn(), replace: vi.fn() })),
  useParams: vi.fn(() => ({})),
}));

import SaudeAdminPage from "../saude/page";

const configAtivo: HealthReportConfigResponse = {
  id: "cfg-1",
  ativo: true,
  horaEnvioUtc: "07:00:00",
  destinatarios: ["ops@forzion.tech"],
  incluirLiveness: true,
  incluirKpis: true,
  incluirEntregabilidade: true,
  incluirErros: true,
  ultimoEnvioEm: null,
};

const configInativo: HealthReportConfigResponse = {
  ...configAtivo,
  ativo: false,
  destinatarios: [],
};

describe("saude-form — validação Zod + submit", () => {
  beforeEach(() => {
    server.use(
      http.get("*/admin/health-report/snapshots", () => HttpResponse.json([])),
    );
  });

  it("ativo=true + email inválido → erro mostrado, PUT não chamado", async () => {
    let putCalled = false;
    server.use(
      http.get("*/admin/health-report/config", () => HttpResponse.json(configAtivo)),
      http.put("*/admin/health-report/config", () => {
        putCalled = true;
        return HttpResponse.json(configAtivo);
      }),
    );
    renderWithProviders(<SaudeAdminPage />);
    await screen.findByText("Relatório de saúde");

    fireEvent.change(screen.getByLabelText(/destinatários/i), {
      target: { value: "nao-e-um-email" },
    });
    fireEvent.click(screen.getByRole("button", { name: /salvar/i }));

    await waitFor(() => {
      expect(screen.getByText("Informe e-mails válidos.")).toBeInTheDocument();
    });
    expect(putCalled).toBe(false);
  });

  it("hora inválida → erro mostrado, PUT não chamado", async () => {
    let putCalled = false;
    server.use(
      http.get("*/admin/health-report/config", () => HttpResponse.json(configInativo)),
      http.put("*/admin/health-report/config", () => {
        putCalled = true;
        return HttpResponse.json(configInativo);
      }),
    );
    renderWithProviders(<SaudeAdminPage />);
    await screen.findByText("Relatório de saúde");

    fireEvent.change(screen.getByLabelText(/hora de envio/i), {
      target: { value: "" },
    });
    fireEvent.click(screen.getByRole("button", { name: /salvar/i }));

    await waitFor(() => {
      expect(screen.getByText("Horário inválido.")).toBeInTheDocument();
    });
    expect(putCalled).toBe(false);
  });

  it("válido: PUT recebe destinatários como array e booleans corretos", async () => {
    let captured: unknown = null;
    server.use(
      http.get("*/admin/health-report/config", () => HttpResponse.json(configAtivo)),
      http.put("*/admin/health-report/config", async ({ request }) => {
        captured = await request.json();
        return HttpResponse.json(configAtivo);
      }),
    );
    renderWithProviders(<SaudeAdminPage />);
    await screen.findByText("Relatório de saúde");

    fireEvent.click(screen.getByRole("button", { name: /salvar/i }));

    await waitFor(() => {
      expect(captured).toMatchObject({
        ativo: true,
        horaEnvioUtc: "07:00:00",
        destinatarios: ["ops@forzion.tech"],
        incluirLiveness: true,
        incluirKpis: true,
        incluirEntregabilidade: true,
        incluirErros: true,
      });
    });
  });
});
