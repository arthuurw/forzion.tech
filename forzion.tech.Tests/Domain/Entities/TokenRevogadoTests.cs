using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class TokenRevogadoTests
{
    [Fact]
    public void Criar_DadosValidos_RetornaTokenRevogado()
    {
        var jti = Guid.NewGuid();
        var agora = DateTime.UtcNow;
        var expiraEm = agora.AddHours(1);

        var token = TokenRevogado.Criar(jti, expiraEm, agora).Value;

        token.Jti.Should().Be(jti);
        token.ExpiraEm.Should().Be(expiraEm);
    }

    [Fact]
    public void Criar_JtiVazio_LancaDomainException()
    {
        var agora = DateTime.UtcNow;
        var r = TokenRevogado.Criar(Guid.Empty, agora.AddHours(1), agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O identificador do token é inválido.");
    }

    [Fact]
    public void Criar_DataExpiracaoPassada_LancaDomainException()
    {
        var agora = DateTime.UtcNow;
        var r = TokenRevogado.Criar(Guid.NewGuid(), agora.AddMinutes(-1), agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("A data de expiração do token deve ser futura.");
    }

    [Fact]
    public void Criar_DataExpiracaoExatamenteAgora_LancaDomainException()
    {
        // guard usa <=, então DateTime.UtcNow exato também deve lançar
        var agora = DateTime.UtcNow;
        var r = TokenRevogado.Criar(Guid.NewGuid(), agora, agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("A data de expiração do token deve ser futura.");
    }
}
