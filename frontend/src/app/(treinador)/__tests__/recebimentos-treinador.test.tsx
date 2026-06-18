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

  it("troca para Plataforma exibe os números reais retornados pelo backend", async () => {
    onboardingHandler("Externo", false);
    server.use(
      http.post("*/treinador/modo-pagamento", () => {
        onboardingHandler("Plataforma", true);
        return HttpResponse.json({ modo: "Plataforma", alteradoEm: new Date().toISOString(), assinaturasCriadas: 3, vinculosIgnorados: 1 });
      }),
    );
    const { default: Page } = await import("@/app/(treinador)/treinador/pagamentos/page");
    render(<Page />);

    fireEvent.click(await screen.findByRole("button", { name: /Voltar a receber pela plataforma/ }));
    fireEvent.click(await screen.findByRole("button", { name: /Voltar à plataforma/ }));

    expect(await screen.findByText(/3 assinatura\(s\) criada\(s\)/)).toBeInTheDocument();
    expect(screen.getByText(/1 vínculo\(s\) sem pacote cobrável/)).toBeInTheDocument();
  });

  it("troca para Externo confirma encerramento das cobranças via plataforma", async () => {
    onboardingHandler("Plataforma", true);
    server.use(
      http.post("*/treinador/modo-pagamento", () => {
        onboardingHandler("Externo", false);
        return HttpResponse.json({ modo: "Externo", alteradoEm: new Date().toISOString(), assinaturasCriadas: 0, vinculosIgnorados: 0 });
      }),
    );
    const { default: Page } = await import("@/app/(treinador)/treinador/pagamentos/page");
    render(<Page />);

    fireEvent.click(await screen.findByRole("button", { name: /Receber por fora da plataforma/ }));
    fireEvent.click(await screen.findByRole("button", { name: /Receber por fora$/ }));

    expect(await screen.findByText(/Cobranças via plataforma encerradas/)).toBeInTheDocument();
  });

  it("diálogo para Externo mostra nº de assinaturas que serão canceladas (preview)", async () => {
    onboardingHandler("Plataforma", true);
    server.use(
      http.get("*/treinador/modo-pagamento/preview", () =>
        HttpResponse.json({ assinaturasAtivasAlunos: 4, vinculosCobravelSemAssinatura: 0 })),
    );
    const { default: Page } = await import("@/app/(treinador)/treinador/pagamentos/page");
    render(<Page />);

    fireEvent.click(await screen.findByRole("button", { name: /Receber por fora da plataforma/ }));

    expect(await screen.findByText(/4 assinatura\(s\) ativa\(s\) de alunos serão canceladas/)).toBeInTheDocument();
  });

  it("diálogo para Plataforma mostra 'até N' assinaturas a criar (preview)", async () => {
    onboardingHandler("Externo", false);
    server.use(
      http.get("*/treinador/modo-pagamento/preview", () =>
        HttpResponse.json({ assinaturasAtivasAlunos: 0, vinculosCobravelSemAssinatura: 5 })),
    );
    const { default: Page } = await import("@/app/(treinador)/treinador/pagamentos/page");
    render(<Page />);

    fireEvent.click(await screen.findByRole("button", { name: /Voltar a receber pela plataforma/ }));

    expect(await screen.findByText(/Até 5 assinatura\(s\) serão criadas/)).toBeInTheDocument();
  });

  it("falha do preview abre o diálogo com aviso e ainda permite confirmar", async () => {
    onboardingHandler("Plataforma", true);
    server.use(
      http.get("*/treinador/modo-pagamento/preview", () =>
        HttpResponse.json({ title: "Erro", status: 500 }, { status: 500 })),
    );
    const { default: Page } = await import("@/app/(treinador)/treinador/pagamentos/page");
    render(<Page />);

    fireEvent.click(await screen.findByRole("button", { name: /Receber por fora da plataforma/ }));

    expect(await screen.findByText(/Não foi possível pré-calcular o impacto/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Receber por fora$/ })).toBeEnabled();
  });

  it("modo Plataforma ativo exibe a taxa da plataforma", async () => {
    onboardingHandler("Plataforma", true);
    const { default: Page } = await import("@/app/(treinador)/treinador/pagamentos/page");
    render(<Page />);

    expect(await screen.findByText(/Taxa da plataforma: 5%/)).toBeInTheDocument();
  });

  it("histórico lista recebimentos com status distinto para estornado", async () => {
    onboardingHandler("Plataforma", true);
    server.use(
      http.get("*/treinador/pagamentos/recebimentos", () =>
        HttpResponse.json({
          itens: [
            { pagamentoId: "p1", bruto: 100, taxaPercent: 5, liquidoEstimado: 95, status: "Pago", nomeAluno: "Ana Lima", metodo: "Pix", createdAt: "2026-05-01T12:00:00Z", dataPagamento: "2026-05-01T12:00:00Z" },
            { pagamentoId: "p2", bruto: 80, taxaPercent: 5, liquidoEstimado: 76, status: "Estornado", nomeAluno: "Bia Souza", metodo: "Pix", createdAt: "2026-04-01T12:00:00Z", dataPagamento: "2026-04-01T12:00:00Z" },
          ],
          proximoCursor: null,
          taxaPlataformaPercent: 5,
        })),
    );
    const { default: Page } = await import("@/app/(treinador)/treinador/pagamentos/page");
    render(<Page />);

    expect(await screen.findByText("Ana Lima")).toBeInTheDocument();
    expect(screen.getByText("Bia Souza")).toBeInTheDocument();
    expect(screen.getByText("Estornado")).toBeInTheDocument();
  });

  it("histórico suprime líquido e taxa em status sem recebimento (Falhou)", async () => {
    onboardingHandler("Plataforma", true);
    server.use(
      http.get("*/treinador/pagamentos/recebimentos", () =>
        HttpResponse.json({
          itens: [
            { pagamentoId: "p9", bruto: 120, taxaPercent: null, liquidoEstimado: null, status: "Falhou", nomeAluno: "Dora Reis", metodo: "Pix", createdAt: "2026-05-01T12:00:00Z", dataPagamento: null },
          ],
          proximoCursor: null,
          taxaPlataformaPercent: 5,
        })),
    );
    const { default: Page } = await import("@/app/(treinador)/treinador/pagamentos/page");
    render(<Page />);

    expect(await screen.findByText("Dora Reis")).toBeInTheDocument();
    expect(screen.getByText(/Líquido estimado: —/)).toBeInTheDocument();
    expect(screen.getByText(/Taxa: —/)).toBeInTheDocument();
  });

  it("histórico vazio mostra estado amigável", async () => {
    onboardingHandler("Plataforma", true);
    server.use(
      http.get("*/treinador/pagamentos/recebimentos", () =>
        HttpResponse.json({ itens: [], proximoCursor: null, taxaPlataformaPercent: 5 })),
    );
    const { default: Page } = await import("@/app/(treinador)/treinador/pagamentos/page");
    render(<Page />);

    expect(await screen.findByText(/Nenhum recebimento ainda/)).toBeInTheDocument();
  });

  it("modo externo não lista recebimentos, mostra placeholder", async () => {
    onboardingHandler("Externo", false);
    const { default: Page } = await import("@/app/(treinador)/treinador/pagamentos/page");
    render(<Page />);

    expect(await screen.findByText(/não há\s+histórico de recebimentos aqui/)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Carregar mais/ })).not.toBeInTheDocument();
  });

  it("histórico carrega próxima página via cursor", async () => {
    onboardingHandler("Plataforma", true);
    let chamadas = 0;
    server.use(
      http.get("*/treinador/pagamentos/recebimentos", ({ request }) => {
        const cursor = new URL(request.url).searchParams.get("cursor");
        chamadas += 1;
        if (!cursor) {
          return HttpResponse.json({
            itens: [{ pagamentoId: "p1", bruto: 100, taxaPercent: 5, liquidoEstimado: 95, status: "Pago", nomeAluno: "Ana Lima", metodo: "Pix", createdAt: "2026-05-01T12:00:00Z", dataPagamento: null }],
            proximoCursor: "C2",
            taxaPlataformaPercent: 5,
          });
        }
        return HttpResponse.json({
          itens: [{ pagamentoId: "p2", bruto: 80, taxaPercent: 5, liquidoEstimado: 76, status: "Pago", nomeAluno: "Bia Souza", metodo: "Pix", createdAt: "2026-04-01T12:00:00Z", dataPagamento: null }],
          proximoCursor: null,
          taxaPlataformaPercent: 5,
        });
      }),
    );
    const { default: Page } = await import("@/app/(treinador)/treinador/pagamentos/page");
    render(<Page />);

    fireEvent.click(await screen.findByRole("button", { name: /Carregar mais/ }));

    expect(await screen.findByText("Bia Souza")).toBeInTheDocument();
    expect(screen.getByText("Ana Lima")).toBeInTheDocument();
    expect(chamadas).toBe(2);
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
