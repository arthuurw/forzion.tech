using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Entities;

public class HealthSnapshotTests
{
    [Fact]
    public void Criar_DadosValidos_RetornaSnapshot()
    {
        var agora = DateTime.UtcNow;

        var snapshot = HealthSnapshot.Criar("homolog", StatusSaude.Ok, "{\"ok\":true}", agora);

        snapshot.Id.Should().NotBeEmpty();
        snapshot.Ambiente.Should().Be("homolog");
        snapshot.StatusGeral.Should().Be(StatusSaude.Ok);
        snapshot.PayloadJson.Should().Be("{\"ok\":true}");
        snapshot.CapturadoEm.Should().Be(agora);
        snapshot.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Criar_AmbienteComEspacos_Remove()
    {
        var snapshot = HealthSnapshot.Criar("  prod  ", StatusSaude.Degradado, "{}", DateTime.UtcNow);
        snapshot.Ambiente.Should().Be("prod");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_AmbienteVazio_LancaDomainException(string ambiente)
    {
        var act = () => HealthSnapshot.Criar(ambiente, StatusSaude.Ok, "{}", DateTime.UtcNow);
        act.Should().Throw<DomainException>().WithMessage("O ambiente é obrigatório.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_PayloadVazio_LancaDomainException(string payload)
    {
        var act = () => HealthSnapshot.Criar("homolog", StatusSaude.Ok, payload, DateTime.UtcNow);
        act.Should().Throw<DomainException>().WithMessage("O payload é obrigatório.");
    }

    [Theory]
    [InlineData(StatusSaude.Ok)]
    [InlineData(StatusSaude.Degradado)]
    [InlineData(StatusSaude.Falha)]
    public void Criar_QualquerStatus_Persistido(StatusSaude status)
    {
        var snapshot = HealthSnapshot.Criar("homolog", status, "{}", DateTime.UtcNow);
        snapshot.StatusGeral.Should().Be(status);
    }
}
