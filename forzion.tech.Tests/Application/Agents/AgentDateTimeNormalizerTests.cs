using FluentAssertions;
using forzion.tech.Application.UseCases.Agents;

namespace forzion.tech.Tests.Application.Agents;

public class AgentDateTimeNormalizerTests
{
    private static readonly DateTime Agora = new(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ParaUtcClampado_Nulo_RetornaAgora()
    {
        var resultado = AgentDateTimeNormalizer.ParaUtcClampado(null, Agora);

        resultado.Should().Be(Agora);
    }

    [Fact]
    public void ParaUtcClampado_KindUnspecified_TratadoComoUtc()
    {
        var valor = new DateTime(2026, 8, 27, 9, 15, 0, DateTimeKind.Unspecified);

        var resultado = AgentDateTimeNormalizer.ParaUtcClampado(valor, Agora);

        resultado.Should().Be(new DateTime(2026, 8, 27, 9, 15, 0, DateTimeKind.Utc));
        resultado.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void ParaUtcClampado_KindLocal_ConvertidoParaUniversal()
    {
        var valorUtc = new DateTime(2026, 8, 27, 9, 15, 0, DateTimeKind.Utc);
        var valorLocal = valorUtc.ToLocalTime();
        valorLocal.Kind.Should().Be(DateTimeKind.Local);

        var resultado = AgentDateTimeNormalizer.ParaUtcClampado(valorLocal, Agora);

        resultado.Should().Be(valorUtc);
    }

    [Fact]
    public void ParaUtcClampado_KindUtc_PermaneceInalterado()
    {
        var valor = new DateTime(2026, 8, 27, 9, 15, 0, DateTimeKind.Utc);

        var resultado = AgentDateTimeNormalizer.ParaUtcClampado(valor, Agora);

        resultado.Should().Be(valor);
    }

    [Fact]
    public void ParaUtcClampado_ValorPosteriorAoServidor_ClampaParaAgora()
    {
        var futuro = Agora.AddDays(30);

        var resultado = AgentDateTimeNormalizer.ParaUtcClampado(futuro, Agora);

        resultado.Should().Be(Agora);
    }

    [Fact]
    public void ParaUtcClampado_ValorAnteriorAoServidor_PreservaValorDeclarado()
    {
        var passado = Agora.AddDays(-1);

        var resultado = AgentDateTimeNormalizer.ParaUtcClampado(passado, Agora);

        resultado.Should().Be(passado);
    }
}
