import React from "react";
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "@/test/msw/server";

function onboardingHandler(
  modo: "Plataforma" | "Externo",
  onboardingCompleto: boolean,
  contaConfigurada = false,
  modoPagamentoPodeAlterarEm: string | null = null,
) {
  server.use(
    http.get("*/treinador/onboarding/status", () =>
      HttpResponse.json({ onboardingCompleto, contaConfigurada, modoPagamentoAluno: modo, modoPagamentoPodeAlterarEm }),
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

  it("modo Plataforma alterna para Externo após confirmação", async () => {
    onboardingHandler("Plataforma", true);
    let chamadaModo: { modo: string } | null = null;
    server.use(
      http.post("*/treinador/modo-pagamento", async ({ request }) => {
        chamadaModo = (await request.json()) as { modo: string };
        // Próxima leitura de status reflete o novo modo.
        onboardingHandler("Externo", false);
        return HttpResponse.json({ modo: "Externo", alteradoEm: new Date().toISOString(), assinaturasCriadas: 0, vinculosIgnorados: 0 });
      }),
    );
    const { default: Page } = await import("@/app/(treinador)/treinador/pagamentos/page");
    render(<Page />);

    fireEvent.click(await screen.findByRole("button", { name: /Receber por fora da plataforma/ }));
    fireEvent.click(await screen.findByRole("button", { name: /Receber por fora$/ }));

    expect(await screen.findByText("Pagamento externo")).toBeInTheDocument();
    expect(chamadaModo).toEqual({ modo: "Externo" });
  });

  it("voltar para plataforma surfaca configure_stripe_primeiro", async () => {
    onboardingHandler("Externo", false);
    server.use(
      http.post("*/treinador/modo-pagamento", () =>
        HttpResponse.json(
          { title: "Erro", detail: "Configure sua conta Stripe antes de voltar a cobrar pela plataforma.", status: 422 },
          { status: 422 },
        ),
      ),
    );
    const { default: Page } = await import("@/app/(treinador)/treinador/pagamentos/page");
    render(<Page />);

    fireEvent.click(await screen.findByRole("button", { name: /Voltar a receber pela plataforma/ }));
    fireEvent.click(await screen.findByRole("button", { name: /Voltar à plataforma/ }));

    expect(
      await screen.findByText("Configure sua conta Stripe antes de voltar a cobrar pela plataforma."),
    ).toBeInTheDocument();
  });

  it("cooldown ativo desabilita a troca e mostra a data de liberação", async () => {
    const podeAlterarEm = new Date(Date.now() + 80 * 24 * 3600 * 1000).toISOString();
    onboardingHandler("Plataforma", true, true, podeAlterarEm);
    const { default: Page } = await import("@/app/(treinador)/treinador/pagamentos/page");
    render(<Page />);

    const botao = await screen.findByRole("button", { name: /Receber por fora da plataforma/ });
    expect(botao).toBeDisabled();
    expect(screen.getByText(/Novo ajuste disponível em/)).toBeInTheDocument();
  });
});
