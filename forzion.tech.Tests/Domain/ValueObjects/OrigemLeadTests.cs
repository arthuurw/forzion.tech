using FluentAssertions;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Tests.Domain.ValueObjects;

public class OrigemLeadTests
{
    [Fact]
    public void Criar_AmbosAusentes_RetornaSucessoComNull()
    {
        var r = OrigemLead.Criar(null, null);

        r.IsSuccess.Should().BeTrue();
        r.Value.Should().BeNull();
    }

    [Fact]
    public void Criar_AmbosVazios_RetornaSucessoComNull()
    {
        var r = OrigemLead.Criar("", "   ");

        r.IsSuccess.Should().BeTrue();
        r.Value.Should().BeNull();
    }

    [Fact]
    public void Criar_ComUserAgent_GuardaValor()
    {
        var r = OrigemLead.Criar("Mozilla/5.0", null);

        r.IsSuccess.Should().BeTrue();
        r.Value!.UserAgent.Should().Be("Mozilla/5.0");
        r.Value.Assistente.Should().BeNull();
    }

    [Fact]
    public void Criar_ComAssistente_GuardaValor()
    {
        var r = OrigemLead.Criar(null, "gateway-gpt");

        r.IsSuccess.Should().BeTrue();
        r.Value!.Assistente.Should().Be("gateway-gpt");
        r.Value.UserAgent.Should().BeNull();
    }

    [Fact]
    public void Criar_UserAgentAcimaDe500_Falha()
    {
        var userAgent = new string('a', 501);

        var r = OrigemLead.Criar(userAgent, null);

        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Contain("500");
    }

    [Fact]
    public void Criar_AssistenteAcimaDe100_Falha()
    {
        var assistente = new string('a', 101);

        var r = OrigemLead.Criar(null, assistente);

        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Contain("100");
    }
}
