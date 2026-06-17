using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Tests.Domain.Entities;

public class MfaChallengeTests
{
    private static readonly DateTime Agora = new(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ContaId = Guid.NewGuid();

    private static MfaChallenge Novo() =>
        MfaChallenge.Criar(ContaId, "hash", MfaProposito.LoginFallback, Agora.AddMinutes(10), Agora).Value;

    [Fact]
    public void Criar_ExpiracaoNaoFutura_RetornaFailure()
    {
        var r = MfaChallenge.Criar(ContaId, "hash", MfaProposito.StepUp, Agora, Agora);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Validar_Valido_Sucesso()
    {
        Novo().Validar(Agora).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validar_Expirado_RetornaFailure()
    {
        Novo().Validar(Agora.AddMinutes(11)).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RegistrarTentativa_AcimaDoCap_Bloqueia()
    {
        var challenge = Novo();

        for (var i = 0; i < MfaChallenge.MaximoTentativas; i++)
            challenge.RegistrarTentativa();

        challenge.Bloqueado.Should().BeTrue();
        challenge.Validar(Agora).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MarcarUsado_Reuso_RetornaFailure()
    {
        var challenge = Novo();
        challenge.MarcarUsado(Agora);

        challenge.MarcarUsado(Agora).IsFailure.Should().BeTrue();
        challenge.Validar(Agora).IsFailure.Should().BeTrue();
    }
}
