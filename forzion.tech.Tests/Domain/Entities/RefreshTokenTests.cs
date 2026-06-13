using FluentAssertions;
using forzion.tech.Domain.Entities;

namespace forzion.tech.Tests.Domain.Entities;

public class RefreshTokenTests
{
    [Fact]
    public void Criar_DadosValidos_RetornaTokenValido()
    {
        var familiaId = Guid.NewGuid();
        var agora = DateTime.UtcNow;

        var token = RefreshToken.Criar(familiaId, "hash", agora.AddDays(7), agora).Value;

        token.FamiliaId.Should().Be(familiaId);
        token.TokenHash.Should().Be("hash");
        token.UsadoEm.Should().BeNull();
        token.SubstituidoPorId.Should().BeNull();
        token.EstaValido(agora).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_HashVazio_Falha(string hash)
    {
        var agora = DateTime.UtcNow;
        var r = RefreshToken.Criar(Guid.NewGuid(), hash, agora.AddDays(7), agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("refresh.token_hash_obrigatorio");
    }

    [Fact]
    public void Criar_ExpiracaoNaoFutura_Falha()
    {
        var agora = DateTime.UtcNow;
        var r = RefreshToken.Criar(Guid.NewGuid(), "hash", agora, agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("refresh.expiracao_nao_futura");
    }

    [Fact]
    public void MarcarUsado_TokenNovo_DefineUsadoESucessor()
    {
        var agora = DateTime.UtcNow;
        var sucessor = Guid.NewGuid();
        var token = RefreshToken.Criar(Guid.NewGuid(), "hash", agora.AddDays(7), agora).Value;

        var r = token.MarcarUsado(agora.AddMinutes(10), sucessor);

        r.IsSuccess.Should().BeTrue();
        token.UsadoEm.Should().Be(agora.AddMinutes(10));
        token.SubstituidoPorId.Should().Be(sucessor);
        token.EstaValido(agora.AddMinutes(10)).Should().BeFalse();
    }

    [Fact]
    public void MarcarUsado_TokenJaUsado_FalhaReuse()
    {
        var agora = DateTime.UtcNow;
        var token = RefreshToken.Criar(Guid.NewGuid(), "hash", agora.AddDays(7), agora).Value;
        token.MarcarUsado(agora, Guid.NewGuid()).IsSuccess.Should().BeTrue();

        var r = token.MarcarUsado(agora, Guid.NewGuid());

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("refresh.token_ja_utilizado");
    }

    [Fact]
    public void MarcarUsado_SucessorVazio_Falha()
    {
        var agora = DateTime.UtcNow;
        var token = RefreshToken.Criar(Guid.NewGuid(), "hash", agora.AddDays(7), agora).Value;

        var r = token.MarcarUsado(agora, Guid.Empty);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("refresh.sucessor_invalido");
    }

    [Fact]
    public void EstaValido_AposIdle_Falso()
    {
        var agora = DateTime.UtcNow;
        var token = RefreshToken.Criar(Guid.NewGuid(), "hash", agora.AddDays(7), agora).Value;

        token.EstaValido(agora.AddDays(8)).Should().BeFalse();
    }
}
