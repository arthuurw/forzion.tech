using FluentAssertions;
using forzion.tech.Domain.Entities;

namespace forzion.tech.Tests.Domain.Entities;

public class MfaRecoveryCodeTests
{
    private static readonly DateTime Agora = new(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ContaId = Guid.NewGuid();

    [Fact]
    public void Criar_DadosValidos_FicaDisponivel()
    {
        var code = MfaRecoveryCode.Criar(ContaId, "hash", Agora).Value;

        code.Disponivel.Should().BeTrue();
        code.UsadoEm.Should().BeNull();
    }

    [Fact]
    public void MarcarUsado_PrimeiraVez_Sucesso()
    {
        var code = MfaRecoveryCode.Criar(ContaId, "hash", Agora).Value;

        code.MarcarUsado(Agora).IsSuccess.Should().BeTrue();
        code.Disponivel.Should().BeFalse();
    }

    [Fact]
    public void MarcarUsado_Reuso_RetornaFailure()
    {
        var code = MfaRecoveryCode.Criar(ContaId, "hash", Agora).Value;
        code.MarcarUsado(Agora);

        code.MarcarUsado(Agora.AddMinutes(1)).IsFailure.Should().BeTrue();
    }
}
