using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Entities;

public class TokenRevogadoTests
{
    [Fact]
    public void Criar_DadosValidos_RetornaTokenRevogado()
    {
        var jti = Guid.NewGuid();
        var expiraEm = DateTime.UtcNow.AddHours(1);

        var token = TokenRevogado.Criar(jti, expiraEm);

        token.Jti.Should().Be(jti);
        token.ExpiraEm.Should().Be(expiraEm);
    }

    [Fact]
    public void Criar_JtiVazio_LancaDomainException()
    {
        var act = () => TokenRevogado.Criar(Guid.Empty, DateTime.UtcNow.AddHours(1));
        act.Should().Throw<DomainException>().WithMessage("O identificador do token é inválido.");
    }

    [Fact]
    public void Criar_DataExpiracaoPassada_LancaDomainException()
    {
        var act = () => TokenRevogado.Criar(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-1));
        act.Should().Throw<DomainException>().WithMessage("A data de expiração do token deve ser futura.");
    }

    [Fact]
    public void Criar_DataExpiracaoExatamenteAgora_LancaDomainException()
    {
        // guard usa <=, então DateTime.UtcNow exato também deve lançar
        var agora = DateTime.UtcNow;
        var act = () => TokenRevogado.Criar(Guid.NewGuid(), agora);
        act.Should().Throw<DomainException>().WithMessage("A data de expiração do token deve ser futura.");
    }
}
