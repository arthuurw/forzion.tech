using FluentAssertions;
using forzion.tech.Infrastructure.Notifications.Email;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class EmailTemplatesLimiteAlunosTests
{
    private static readonly DateTime DataLimite = new(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);
    private const string LinkPortal = "https://app.forzion.tech/treinador/plano";

    [Fact]
    public void LimiteAlunosExcedido_ContemExcedenteEDataLimiteFormatada()
    {
        var html = EmailTemplates.LimiteAlunosExcedido("Ana Treinadora", 4, DataLimite, LinkPortal);

        html.Should().Contain("4");
        html.Should().Contain("15/09/2026");
        html.Should().Contain("Ana Treinadora");
        html.Should().Contain(LinkPortal);
    }

    [Fact]
    public void LimiteAlunosLembrete_ContemExcedenteEDataLimiteFormatada()
    {
        var html = EmailTemplates.LimiteAlunosLembrete("Ana Treinadora", 2, DataLimite, LinkPortal);

        html.Should().Contain("2");
        html.Should().Contain("15/09/2026");
        html.Should().Contain("Ana Treinadora");
        html.Should().Contain(LinkPortal);
    }

    [Fact]
    public void LimiteAlunosAplicado_ContemQuantidadeDesativada()
    {
        var html = EmailTemplates.LimiteAlunosAplicado("Ana Treinadora", 3, LinkPortal);

        html.Should().Contain("3");
        html.Should().Contain("desativados automaticamente");
        html.Should().Contain("Ana Treinadora");
        html.Should().Contain(LinkPortal);
    }

    [Fact]
    public void LimiteAlunosExcedido_EscapaNomeDoTreinador()
    {
        var html = EmailTemplates.LimiteAlunosExcedido("<script>alert(1)</script>", 1, DataLimite, LinkPortal);

        html.Should().NotContain("<script>");
        html.Should().Contain("&lt;script&gt;");
    }
}
