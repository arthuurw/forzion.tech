using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class HealthSnapshotTests
{
    [Fact]
    public void Criar_DadosValidos_RetornaSnapshot()
    {
        var agora = TestData.Agora;

        var snapshot = HealthSnapshot.Criar("homolog", StatusSaude.Ok, "{\"ok\":true}", agora).Value;

        snapshot.Id.Should().NotBeEmpty();
        snapshot.Ambiente.Should().Be("homolog");
        snapshot.StatusGeral.Should().Be(StatusSaude.Ok);
        snapshot.PayloadJson.Should().Be("{\"ok\":true}");
        snapshot.CapturadoEm.Should().Be(agora);
        snapshot.CreatedAt.Should().BeCloseTo(TestData.Agora, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Criar_AmbienteComEspacos_Remove()
    {
        var snapshot = HealthSnapshot.Criar("  prod  ", StatusSaude.Degradado, "{}", TestData.Agora).Value;
        snapshot.Ambiente.Should().Be("prod");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_AmbienteVazio_LancaDomainException(string ambiente)
    {
        var r = HealthSnapshot.Criar(ambiente, StatusSaude.Ok, "{}", TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O ambiente é obrigatório.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_PayloadVazio_LancaDomainException(string payload)
    {
        var r = HealthSnapshot.Criar("homolog", StatusSaude.Ok, payload, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O payload é obrigatório.");
    }

    [Theory]
    [InlineData(StatusSaude.Ok)]
    [InlineData(StatusSaude.Degradado)]
    [InlineData(StatusSaude.Falha)]
    public void Criar_QualquerStatus_Persistido(StatusSaude status)
    {
        var snapshot = HealthSnapshot.Criar("homolog", status, "{}", TestData.Agora).Value;
        snapshot.StatusGeral.Should().Be(status);
    }
}
