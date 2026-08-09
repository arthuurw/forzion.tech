using forzion.tech.Infrastructure.Logging;
using FluentAssertions;

namespace forzion.tech.Tests.Infrastructure.Logging;

public class AlertaSegurancaSentryNuloTests
{
    [Fact]
    public void Registrar_QualquerEntrada_NaoLanca()
    {
        var sink = new AlertaSegurancaSentryNulo();

        var act = () => sink.Registrar("refresh_reuse", "mensagem", new Dictionary<string, string> { ["FamiliaId"] = "abc" });

        act.Should().NotThrow();
    }
}
