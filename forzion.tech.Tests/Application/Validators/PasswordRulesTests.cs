using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Validation;

namespace forzion.tech.Tests.Application.Validators;

public class PasswordRulesTests
{
    private sealed record Alvo(string Senha);

    private sealed class AlvoValidator : AbstractValidator<Alvo>
    {
        public AlvoValidator() => RuleFor(x => x.Senha).SenhaForte();
    }

    private static readonly AlvoValidator Validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("Aa1aaaa")]
    [InlineData("senha123")]
    [InlineData("SENHA123")]
    [InlineData("SenhaAbc")]
    public void Rejeita_SenhaForaDaPolitica(string senha) =>
        Validator.Validate(new Alvo(senha)).IsValid.Should().BeFalse();

    [Fact]
    public void Rejeita_AcimaDe72Chars() =>
        Validator.Validate(new Alvo("Sa1" + new string('x', 70))).IsValid.Should().BeFalse();

    [Fact]
    public void Aceita_SenhaForte() =>
        Validator.Validate(new Alvo("Senha123")).IsValid.Should().BeTrue();
}
