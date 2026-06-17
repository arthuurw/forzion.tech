using FluentAssertions;
using forzion.tech.Domain.Entities;

namespace forzion.tech.Tests.Domain.Entities;

public class TrustedDeviceTests
{
    private static readonly DateTime Agora = new(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ContaId = Guid.NewGuid();

    private static TrustedDevice Novo() =>
        TrustedDevice.Criar(ContaId, "tokenhash", Agora.AddDays(30), Agora, "Chrome/Windows").Value;

    [Fact]
    public void Criar_ExpiracaoNaoFutura_RetornaFailure()
    {
        var r = TrustedDevice.Criar(ContaId, "tokenhash", Agora, Agora);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void EstaAtivo_DentroDaValidade_True()
    {
        Novo().EstaAtivo(Agora.AddDays(10)).Should().BeTrue();
    }

    [Fact]
    public void EstaAtivo_Expirado_False()
    {
        Novo().EstaAtivo(Agora.AddDays(31)).Should().BeFalse();
    }

    [Fact]
    public void Revogar_TornaInativo()
    {
        var device = Novo();

        device.Revogar(Agora.AddDays(1)).IsSuccess.Should().BeTrue();
        device.EstaAtivo(Agora.AddDays(2)).Should().BeFalse();
    }

    [Fact]
    public void Revogar_JaRevogado_RetornaFailure()
    {
        var device = Novo();
        device.Revogar(Agora.AddDays(1));

        device.Revogar(Agora.AddDays(2)).IsFailure.Should().BeTrue();
    }
}
