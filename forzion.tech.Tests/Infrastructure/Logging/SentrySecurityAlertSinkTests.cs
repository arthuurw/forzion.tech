using forzion.tech.Infrastructure.Logging;
using FluentAssertions;

namespace forzion.tech.Tests.Infrastructure.Logging;

public class SentrySecurityAlertSinkTests
{
    [Fact]
    public void Registrar_SdkNaoInicializado_NaoLanca()
    {
        var sink = new SentrySecurityAlertSink();

        var act = () => sink.Registrar(
            "refresh_reuse",
            "Reuse de refresh token detectado",
            new Dictionary<string, string> { ["FamiliaId"] = "abc", ["ContaId"] = "def" });

        act.Should().NotThrow();
    }
}
