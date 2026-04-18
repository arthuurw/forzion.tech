using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Tests.Domain.Entities;

public class ContaTests
{
    private static readonly Email EmailValido = Email.Criar("treinador@email.com");
    private const string HashValido = "$2a$12$abcdefghijklmnopqrstuuVGrfHSMr6yp6vQI1234567890abcdef";

    [Fact]
    public void Criar_DadosValidos_RetornaContaCorreta()
    {
        var conta = Conta.Criar(EmailValido, HashValido, TipoConta.Treinador);

        conta.Id.Should().NotBe(Guid.Empty);
        conta.Email.Should().Be(EmailValido);
        conta.PasswordHash.Should().Be(HashValido);
        conta.TipoConta.Should().Be(TipoConta.Treinador);
        conta.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        conta.UpdatedAt.Should().BeNull();
    }

    [Theory]
    [InlineData(TipoConta.SystemAdmin)]
    [InlineData(TipoConta.Treinador)]
    [InlineData(TipoConta.Aluno)]
    public void Criar_TodosOsTipos_CriaCorretamente(TipoConta tipo)
    {
        var conta = Conta.Criar(EmailValido, HashValido, tipo);
        conta.TipoConta.Should().Be(tipo);
    }

    [Fact]
    public void Criar_EmailNulo_LancaArgumentNullException()
    {
        var act = () => Conta.Criar(null!, HashValido, TipoConta.Treinador);
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_PasswordHashVazio_LancaDomainException(string hash)
    {
        var act = () => Conta.Criar(EmailValido, hash, TipoConta.Treinador);
        act.Should().Throw<DomainException>().WithMessage("*hash da senha*");
    }

    [Fact]
    public void AtualizarSenha_NovoHashValido_AtualizaEPreencheUpdatedAt()
    {
        var conta = Conta.Criar(EmailValido, HashValido, TipoConta.Aluno);
        var novoHash = "$2a$12$zyxwvutsrqponmlkjihgffeKLMNOPQRSTUVWXYZ0123456789abcd";

        conta.AtualizarSenha(novoHash);

        conta.PasswordHash.Should().Be(novoHash);
        conta.UpdatedAt.Should().NotBeNull();
        conta.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AtualizarSenha_HashVazio_LancaDomainException(string hash)
    {
        var conta = Conta.Criar(EmailValido, HashValido, TipoConta.Treinador);
        var act = () => conta.AtualizarSenha(hash);
        act.Should().Throw<DomainException>().WithMessage("*hash da senha*");
    }
}
