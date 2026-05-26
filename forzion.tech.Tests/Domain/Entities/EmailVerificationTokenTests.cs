using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Entities;

public class EmailVerificationTokenTests
{
    private static readonly DateTime Agora = new(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ContaId = Guid.NewGuid();
    private const string HashValido = "abc123";

    [Fact]
    public void Criar_DadosValidos_RetornaTokenCorreto()
    {
        var token = EmailVerificationToken.Criar(ContaId, HashValido, Agora.AddHours(24), Agora);

        token.Id.Should().NotBe(Guid.Empty);
        token.ContaId.Should().Be(ContaId);
        token.TokenHash.Should().Be(HashValido);
        token.ExpiresAt.Should().Be(Agora.AddHours(24));
        token.CreatedAt.Should().Be(Agora);
        token.VerifiedAt.Should().BeNull();
    }

    [Fact]
    public void Criar_ContaIdVazio_LancaDomainException()
    {
        var act = () => EmailVerificationToken.Criar(Guid.Empty, HashValido, Agora.AddHours(24), Agora);
        act.Should().Throw<DomainException>().WithMessage("*identificador da conta*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_TokenHashVazio_LancaDomainException(string hash)
    {
        var act = () => EmailVerificationToken.Criar(ContaId, hash, Agora.AddHours(24), Agora);
        act.Should().Throw<DomainException>().WithMessage("*hash do token*");
    }

    [Fact]
    public void Criar_ExpiracaoNaoFutura_LancaDomainException()
    {
        var act = () => EmailVerificationToken.Criar(ContaId, HashValido, Agora, Agora);
        act.Should().Throw<DomainException>().WithMessage("*expiração deve ser futura*");
    }

    [Fact]
    public void MarcarComoVerificado_TokenNovo_PreencheVerifiedAt()
    {
        var token = EmailVerificationToken.Criar(ContaId, HashValido, Agora.AddHours(24), Agora);

        token.MarcarComoVerificado(Agora.AddMinutes(5));

        token.VerifiedAt.Should().Be(Agora.AddMinutes(5));
    }

    [Fact]
    public void MarcarComoVerificado_TokenJaUtilizado_LancaDomainException()
    {
        var token = EmailVerificationToken.Criar(ContaId, HashValido, Agora.AddHours(24), Agora);
        token.MarcarComoVerificado(Agora.AddMinutes(5));

        var act = () => token.MarcarComoVerificado(Agora.AddMinutes(10));

        act.Should().Throw<DomainException>().WithMessage("*já foi utilizado*");
    }
}
