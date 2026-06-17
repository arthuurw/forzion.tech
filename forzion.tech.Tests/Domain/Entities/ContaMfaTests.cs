using FluentAssertions;
using forzion.tech.Domain.Entities;

namespace forzion.tech.Tests.Domain.Entities;

public class ContaMfaTests
{
    private static readonly DateTime Agora = new(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ContaId = Guid.NewGuid();
    private const string SecretCifrado = "cifrado==";

    [Fact]
    public void Criar_DadosValidos_FicaPendente()
    {
        var mfa = ContaMfa.Criar(ContaId, SecretCifrado, Agora).Value;

        mfa.ContaId.Should().Be(ContaId);
        mfa.Habilitado.Should().BeFalse();
        mfa.ConfirmadoEm.Should().BeNull();
        mfa.TotpSecretCifrado.Should().Be(SecretCifrado);
    }

    [Fact]
    public void Criar_ContaIdVazio_RetornaFailure()
    {
        var r = ContaMfa.Criar(Guid.Empty, SecretCifrado, Agora);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Criar_SecretVazio_RetornaFailure()
    {
        var r = ContaMfa.Criar(ContaId, "  ", Agora);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Confirmar_PendentE_Habilita()
    {
        var mfa = ContaMfa.Criar(ContaId, SecretCifrado, Agora).Value;

        var r = mfa.Confirmar(100, Agora);

        r.IsSuccess.Should().BeTrue();
        mfa.Habilitado.Should().BeTrue();
        mfa.ConfirmadoEm.Should().Be(Agora);
        mfa.UltimoTimeStep.Should().Be(100);
    }

    [Fact]
    public void Confirmar_JaConfirmado_RetornaFailure()
    {
        var mfa = ContaMfa.Criar(ContaId, SecretCifrado, Agora).Value;
        mfa.Confirmar(100, Agora);

        var r = mfa.Confirmar(101, Agora);

        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RegistrarUso_TimeStepMenorOuIgualUltimo_RejeitaReplay()
    {
        var mfa = ContaMfa.Criar(ContaId, SecretCifrado, Agora).Value;
        mfa.Confirmar(100, Agora);

        mfa.RegistrarUso(100, Agora).IsFailure.Should().BeTrue();
        mfa.RegistrarUso(99, Agora).IsFailure.Should().BeTrue();
        mfa.RegistrarUso(101, Agora).IsSuccess.Should().BeTrue();
        mfa.UltimoTimeStep.Should().Be(101);
    }

    [Fact]
    public void RegistrarUso_NaoHabilitado_RetornaFailure()
    {
        var mfa = ContaMfa.Criar(ContaId, SecretCifrado, Agora).Value;

        mfa.RegistrarUso(100, Agora).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Desabilitar_LimpaEstado()
    {
        var mfa = ContaMfa.Criar(ContaId, SecretCifrado, Agora).Value;
        mfa.Confirmar(100, Agora);

        mfa.Desabilitar(Agora.AddMinutes(1));

        mfa.Habilitado.Should().BeFalse();
        mfa.TotpSecretCifrado.Should().BeNull();
        mfa.ConfirmadoEm.Should().BeNull();
        mfa.UltimoTimeStep.Should().BeNull();
    }
}
