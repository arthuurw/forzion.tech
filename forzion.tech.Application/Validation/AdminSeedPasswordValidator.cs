using FluentValidation;
using forzion.tech.Application.Interfaces;

namespace forzion.tech.Application.Validation;

public class AdminSeedPasswordValidator : AbstractValidator<string>
{
    public AdminSeedPasswordValidator(IPwnedPasswordsService pwnedPasswords)
    {
        RuleFor(x => x).SenhaForte().SenhaNaoComprometida(pwnedPasswords);
    }
}
