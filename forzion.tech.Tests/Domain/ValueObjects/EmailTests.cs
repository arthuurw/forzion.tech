using FluentAssertions;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Tests.Domain.ValueObjects;

public class EmailTests
{
    [Theory]
    [InlineData("user@example.com")]
    [InlineData("user.name+tag@domain.co.uk")]
    [InlineData("a@b.io")]
    public void Criar_ComEmailValido_RetornaEmail(string email)
    {
        var result = Email.Criar(email);
        result.Value.Should().Be(email.Trim().ToLowerInvariant());
    }

    [Fact]
    public void Criar_ComEmailEmMaiusculo_NormalizaParaMinusculo()
    {
        var result = Email.Criar("USER@EXAMPLE.COM");
        result.Value.Should().Be("user@example.com");
    }

    [Fact]
    public void Criar_ComEspacos_Remove()
    {
        var result = Email.Criar("  user@example.com  ");
        result.Value.Should().Be("user@example.com");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_ComEmailVazioOuEspacos_LancaDomainException(string email)
    {
        var act = () => Email.Criar(email);
        act.Should().Throw<DomainException>().WithMessage("O e-mail é obrigatório.");
    }

    [Fact]
    public void Criar_ComEmailMuitoLongo_LancaDomainException()
    {
        var email = new string('a', 251) + "@b.com";
        var act = () => Email.Criar(email);
        act.Should().Throw<DomainException>().WithMessage("O e-mail deve ter no máximo 256 caracteres.");
    }

    [Theory]
    [InlineData("invalido")]
    [InlineData("sem-arroba.com")]
    [InlineData("@semlocal.com")]
    [InlineData("sem@ponto")]
    public void Criar_ComFormatoInvalido_LancaDomainException(string email)
    {
        var act = () => Email.Criar(email);
        act.Should().Throw<DomainException>().WithMessage("O e-mail informado é inválido.");
    }

    [Fact]
    public void FromDatabase_RetornaEmailSemValidacao()
    {
        var result = Email.FromDatabase("valor-qualquer");
        result.Value.Should().Be("valor-qualquer");
    }

    [Fact]
    public void ToString_RetornaValue()
    {
        var email = Email.Criar("user@example.com");
        email.ToString().Should().Be("user@example.com");
    }
}
