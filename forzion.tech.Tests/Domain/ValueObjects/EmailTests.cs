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
        result.Value.Value.Should().Be(email.Trim().ToLowerInvariant());
    }

    [Fact]
    public void Criar_ComEmailEmMaiusculo_NormalizaParaMinusculo()
    {
        var result = Email.Criar("USER@EXAMPLE.COM");
        result.Value.Value.Should().Be("user@example.com");
    }

    [Fact]
    public void Criar_ComEspacos_Remove()
    {
        var result = Email.Criar("  user@example.com  ");
        result.Value.Value.Should().Be("user@example.com");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_ComEmailVazioOuEspacos_LancaDomainException(string email)
    {
        var r = Email.Criar(email);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O e-mail é obrigatório.");
    }

    [Fact]
    public void Criar_ComEmailMuitoLongo_LancaDomainException()
    {
        var email = new string('a', 251) + "@b.com";
        var r = Email.Criar(email);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O e-mail deve ter no máximo 256 caracteres.");
    }

    [Theory]
    [InlineData("invalido")]
    [InlineData("sem-arroba.com")]
    [InlineData("@semlocal.com")]
    [InlineData("sem@ponto")]
    public void Criar_ComFormatoInvalido_LancaDomainException(string email)
    {
        var r = Email.Criar(email);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O e-mail informado é inválido.");
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
        var email = Email.Criar("user@example.com").Value;
        email.ToString().Should().Be("user@example.com");
    }
}
