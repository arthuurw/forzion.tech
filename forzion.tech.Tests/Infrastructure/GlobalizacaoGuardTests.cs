using FluentAssertions;

namespace forzion.tech.Tests.Infrastructure;

public class GlobalizacaoGuardTests
{
    [Fact]
    public void FusoSemDst_America_Sao_Paulo_Resolve_SenaoTodaDerivacaoDeDisponibilidadeVira503()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

        tz.Id.Should().Be("America/Sao_Paulo");
    }

    [Fact]
    public void FusoComDst_America_New_York_Resolve_SenaoBaseDeTzdataEstaIncompleta()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        tz.Id.Should().Be("America/New_York");
        tz.SupportsDaylightSavingTime.Should().BeTrue();
    }
}
