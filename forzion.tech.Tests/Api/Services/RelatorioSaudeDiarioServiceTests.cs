using FluentAssertions;
using forzion.tech.Api.Services;
using forzion.tech.Domain.Entities;

namespace forzion.tech.Tests.Api.Services;

public class RelatorioSaudeDiarioServiceTests
{
    private static readonly TimeOnly Hora = new(7, 0);

    private static HealthReportConfig Config(bool ativo) =>
        HealthReportConfig.Criar(
            ativo,
            Hora,
            ativo ? new[] { "admin@forzion.tech" } : Array.Empty<string>(),
            true, true, true, true,
            DateTime.UtcNow).Value;

    [Fact]
    public void DeveEnviar_Inativo_RetornaFalse()
    {
        var config = Config(ativo: false);
        var agora = new DateTime(2026, 5, 26, 8, 0, 0, DateTimeKind.Utc);

        RelatorioSaudeDiarioService.DeveEnviar(config, agora).Should().BeFalse();
    }

    [Fact]
    public void DeveEnviar_AntesDaHora_RetornaFalse()
    {
        var config = Config(ativo: true);
        var agora = new DateTime(2026, 5, 26, 6, 59, 0, DateTimeKind.Utc);

        RelatorioSaudeDiarioService.DeveEnviar(config, agora).Should().BeFalse();
    }

    [Fact]
    public void DeveEnviar_DevidoENaoEnviado_RetornaTrue()
    {
        var config = Config(ativo: true);
        var agora = new DateTime(2026, 5, 26, 7, 30, 0, DateTimeKind.Utc);

        RelatorioSaudeDiarioService.DeveEnviar(config, agora).Should().BeTrue();
    }

    [Fact]
    public void DeveEnviar_JaEnviadoHoje_RetornaFalse()
    {
        var config = Config(ativo: true);
        config.MarcarEnviado(new DateTime(2026, 5, 26, 7, 0, 0, DateTimeKind.Utc));
        var agora = new DateTime(2026, 5, 26, 7, 30, 0, DateTimeKind.Utc);

        RelatorioSaudeDiarioService.DeveEnviar(config, agora).Should().BeFalse();
    }

    [Fact]
    public void DeveEnviar_EnviadoOntem_RetornaTrue()
    {
        var config = Config(ativo: true);
        config.MarcarEnviado(new DateTime(2026, 5, 25, 7, 0, 0, DateTimeKind.Utc));
        var agora = new DateTime(2026, 5, 26, 7, 30, 0, DateTimeKind.Utc);

        RelatorioSaudeDiarioService.DeveEnviar(config, agora).Should().BeTrue();
    }
}
