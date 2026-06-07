import React from "react";
import { describe, it, expect } from "vitest";
import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";

function onboardingHandler(modo: "Plataforma" | "Externo", onboardingCompleto: boolean, contaConfigurada = false) {
  server.use(
    http.get("*/treinador/onboarding/status", () =>
      HttpResponse.json({ onboardingCompleto, contaConfigurada, modoPagamentoAluno: modo }),
    ),
  );
}

describe("PagamentosTreinadorPage (Recebimentos)", () => {
  it("modo Externo exibe orientação de controle manual sem onboarding Stripe", async () => {
    onboardingHandler("Externo", false);
    const { default: Page } = await import("@/app/(treinador)/treinador/pagamentos/page");
    render(<Page />);

    expect(await screen.findByText(/por fora da plataforma/)).toBeInTheDocument();
    expect(screen.getByText("Pagamento externo")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Configurar recebimentos/ })).not.toBeInTheDocument();
  });

  it("modo Plataforma não configurado exibe onboarding Stripe", async () => {
    onboardingHandler("Plataforma", false);
    const { default: Page } = await import("@/app/(treinador)/treinador/pagamentos/page");
    render(<Page />);

    expect(await screen.findByRole("button", { name: /Configurar recebimentos/ })).toBeInTheDocument();
  });

  it("falha no onboarding surfaca o detalhe real do backend", async () => {
    onboardingHandler("Plataforma", false);
    server.use(
      http.post("*/treinador/onboarding", () =>
        HttpResponse.json(
          { title: "Erro", detail: "Configuração de pagamento indisponível. Contate o suporte.", status: 500 },
          { status: 500 },
        ),
      ),
    );
    const { default: Page } = await import("@/app/(treinador)/treinador/pagamentos/page");
    render(<Page />);

    fireEvent.click(await screen.findByRole("button", { name: /Configurar recebimentos/ }));

    expect(await screen.findByText("Configuração de pagamento indisponível. Contate o suporte.")).toBeInTheDocument();
  });
});
