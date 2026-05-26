using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Entities;

public class HealthReportConfigTests
{
    private static readonly TimeOnly Hora = new(7, 0);

    // --- Criar ---

    [Fact]
    public void Criar_DadosValidos_RetornaConfig()
    {
        var config = HealthReportConfig.Criar(
            ativo: true,
            horaEnvioUtc: Hora,
            destinatarios: new[] { "admin@forzion.tech" },
            incluirLiveness: true,
            incluirKpis: true,
            incluirEntregabilidade: true,
            incluirErros: true,
            agora: DateTime.UtcNow);

        config.Id.Should().NotBeEmpty();
        config.Ativo.Should().BeTrue();
        config.HoraEnvioUtc.Should().Be(Hora);
        config.Destinatarios.Should().Be("admin@forzion.tech");
        config.IncluirLiveness.Should().BeTrue();
        config.IncluirKpis.Should().BeTrue();
        config.IncluirEntregabilidade.Should().BeTrue();
        config.IncluirErros.Should().BeTrue();
        config.UltimoEnvioEm.Should().BeNull();
        config.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        config.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Criar_InativoSemDestinatarios_Permitido()
    {
        var config = HealthReportConfig.Criar(
            ativo: false,
            horaEnvioUtc: Hora,
            destinatarios: Array.Empty<string>(),
            incluirLiveness: true,
            incluirKpis: true,
            incluirEntregabilidade: false,
            incluirErros: false,
            agora: DateTime.UtcNow);

        config.Ativo.Should().BeFalse();
        config.Destinatarios.Should().BeEmpty();
    }

    [Fact]
    public void Criar_AtivoSemDestinatarios_LancaDomainException()
    {
        var act = () => HealthReportConfig.Criar(
            ativo: true,
            horaEnvioUtc: Hora,
            destinatarios: Array.Empty<string>(),
            incluirLiveness: true,
            incluirKpis: true,
            incluirEntregabilidade: true,
            incluirErros: true,
            agora: DateTime.UtcNow);

        act.Should().Throw<DomainException>()
            .WithMessage("Uma configuração ativa exige ao menos um destinatário.");
    }

    [Fact]
    public void Criar_EmailInvalido_LancaDomainException()
    {
        var act = () => HealthReportConfig.Criar(
            ativo: true,
            horaEnvioUtc: Hora,
            destinatarios: new[] { "nao-eh-email" },
            incluirLiveness: true,
            incluirKpis: true,
            incluirEntregabilidade: true,
            incluirErros: true,
            agora: DateTime.UtcNow);

        act.Should().Throw<DomainException>().WithMessage("O e-mail informado é inválido.");
    }

    [Fact]
    public void Criar_NormalizaTrimLowercaseEDeduplica()
    {
        var config = HealthReportConfig.Criar(
            ativo: true,
            horaEnvioUtc: Hora,
            destinatarios: new[] { "  Admin@Forzion.Tech ", "admin@forzion.tech", "ops@forzion.tech", "" },
            incluirLiveness: true,
            incluirKpis: true,
            incluirEntregabilidade: true,
            incluirErros: true,
            agora: DateTime.UtcNow);

        config.Destinatarios.Should().Be("admin@forzion.tech,ops@forzion.tech");
        config.ObterDestinatarios().Should().Equal("admin@forzion.tech", "ops@forzion.tech");
    }

    // --- Atualizar ---

    [Fact]
    public void Atualizar_DadosValidos_AtualizaCamposESetaUpdatedAt()
    {
        var config = HealthReportConfig.Criar(
            ativo: false,
            horaEnvioUtc: Hora,
            destinatarios: Array.Empty<string>(),
            incluirLiveness: false,
            incluirKpis: false,
            incluirEntregabilidade: false,
            incluirErros: false,
            agora: DateTime.UtcNow);

        var novaHora = new TimeOnly(9, 30);
        config.Atualizar(
            ativo: true,
            horaEnvioUtc: novaHora,
            destinatarios: new[] { "ops@forzion.tech" },
            incluirLiveness: true,
            incluirKpis: true,
            incluirEntregabilidade: true,
            incluirErros: true);

        config.Ativo.Should().BeTrue();
        config.HoraEnvioUtc.Should().Be(novaHora);
        config.Destinatarios.Should().Be("ops@forzion.tech");
        config.IncluirLiveness.Should().BeTrue();
        config.IncluirKpis.Should().BeTrue();
        config.IncluirEntregabilidade.Should().BeTrue();
        config.IncluirErros.Should().BeTrue();
        config.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Atualizar_AtivoSemDestinatarios_LancaDomainException()
    {
        var config = HealthReportConfig.Criar(
            ativo: false,
            horaEnvioUtc: Hora,
            destinatarios: Array.Empty<string>(),
            incluirLiveness: true,
            incluirKpis: true,
            incluirEntregabilidade: true,
            incluirErros: true,
            agora: DateTime.UtcNow);

        var act = () => config.Atualizar(
            ativo: true,
            horaEnvioUtc: Hora,
            destinatarios: Array.Empty<string>(),
            incluirLiveness: true,
            incluirKpis: true,
            incluirEntregabilidade: true,
            incluirErros: true);

        act.Should().Throw<DomainException>()
            .WithMessage("Uma configuração ativa exige ao menos um destinatário.");
    }

    // --- MarcarEnviado ---

    [Fact]
    public void MarcarEnviado_SetaUltimoEnvioEmEUpdatedAt()
    {
        var config = HealthReportConfig.Criar(
            ativo: true,
            horaEnvioUtc: Hora,
            destinatarios: new[] { "admin@forzion.tech" },
            incluirLiveness: true,
            incluirKpis: true,
            incluirEntregabilidade: true,
            incluirErros: true,
            agora: DateTime.UtcNow);

        var envio = new DateTime(2026, 5, 26, 7, 0, 0, DateTimeKind.Utc);
        config.MarcarEnviado(envio);

        config.UltimoEnvioEm.Should().Be(envio);
        config.UpdatedAt.Should().NotBeNull();
    }
}
