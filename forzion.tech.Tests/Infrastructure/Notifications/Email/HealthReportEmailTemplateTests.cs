using FluentAssertions;
using forzion.tech.Application.UseCases.Admin.HealthReport;
using forzion.tech.Domain.Enums;
using forzion.tech.Infrastructure.Notifications.Email;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class HealthReportEmailTemplateTests
{
    private static readonly DateTime Agora = new(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);

    private static HealthReport Report(
        StatusSaude status = StatusSaude.Ok,
        bool comLiveness = true,
        bool comKpis = true,
        bool comEntrega = true,
        ErrosSecao? erros = null) =>
        new()
        {
            Ambiente = "Homolog",
            CapturadoEm = Agora,
            StatusGeral = status,
            Liveness = comLiveness
                ? new LivenessSecao
                {
                    BancoAcessivel = true,
                    EmailHabilitado = true,
                    StripeConfigurado = true,
                    WhatsAppConfigurado = false,
                    Versao = "1.2.3",
                    Commit = "abc123"
                }
                : null,
            Kpis = comKpis
                ? new KpisSecao
                {
                    TreinadoresAtivos = 7,
                    AlunosAtivos = 11,
                    NovasContas24h = 3,
                    PagamentosPendentes = 2,
                    PagamentosFalhos = 4,
                    AssinaturasAtivas = 9
                }
                : null,
            Entregabilidade = comEntrega
                ? new EntregabilidadeSecao { Total = 12, Entregues = 5, Bounces = 2, Spam = 2 }
                : null,
            Erros = erros
        };

    [Fact]
    public void RelatorioSaude_ComTodasSecoes_RenderizaConteudo()
    {
        var report = Report(erros: new ErrosSecao { Total = 0, Amostras = Array.Empty<ErroAmostra>() });

        var html = EmailTemplates.RelatorioSaude(report);

        html.Should().Contain("<!DOCTYPE html>");
        html.Should().Contain("Relatório de saúde");
        html.Should().Contain("Homolog");
        html.Should().Contain("Ok");
        html.Should().Contain("Infraestrutura");
        html.Should().Contain("Indicadores");
        html.Should().Contain("Treinadores ativos");
        html.Should().Contain("7");
        html.Should().Contain("Entregabilidade de e-mail");
        html.Should().Contain("Erros (24h)");
        html.Should().Contain("Nenhum erro registrado");
    }

    [Fact]
    public void RelatorioSaude_SemSecoes_OmiteCabecalhos()
    {
        var report = Report(comLiveness: false, comKpis: false, comEntrega: false, erros: null);

        var html = EmailTemplates.RelatorioSaude(report);

        html.Should().Contain("Homolog");
        html.Should().NotContain("Infraestrutura");
        html.Should().NotContain("Indicadores");
        html.Should().NotContain("Entregabilidade de e-mail");
        html.Should().NotContain("Erros (24h)");
    }

    [Fact]
    public void RelatorioSaude_MensagemDeErro_EhEscapada()
    {
        var erros = new ErrosSecao
        {
            Total = 1,
            Amostras = new[]
            {
                new ErroAmostra
                {
                    OcorridoEm = Agora,
                    Nivel = "Error",
                    Origem = "Worker",
                    Mensagem = "<script>alert('xss')</script>"
                }
            }
        };
        var report = Report(comLiveness: false, comKpis: false, comEntrega: false, erros: erros);

        var html = EmailTemplates.RelatorioSaude(report);

        html.Should().Contain("&lt;script&gt;");
        html.Should().NotContain("<script>alert");
    }
}
