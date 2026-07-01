using FluentValidation;
using forzion.tech.Application.Interfaces;

namespace forzion.tech.Application.Validation;

public static class PasswordRules
{
    public static IRuleBuilderOptions<T, string> SenhaForte<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("A senha é obrigatória.")
            .MinimumLength(12).WithMessage("A senha deve ter pelo menos 12 caracteres.")
            .MaximumLength(72).WithMessage("A senha deve ter no máximo 72 caracteres.")
            .Matches("[a-z]").WithMessage("A senha deve conter pelo menos uma letra minúscula.")
            .Matches("[A-Z]").WithMessage("A senha deve conter pelo menos uma letra maiúscula.")
            .Matches("[0-9]").WithMessage("A senha deve conter pelo menos um dígito.");

    public static IRuleBuilderOptions<T, string> SenhaNaoComprometida<T>(
        this IRuleBuilder<T, string> rule, IPwnedPasswordsService pwnedPasswords) =>
        rule.MustAsync(async (senha, ct) =>
                !await pwnedPasswords.EstaComprometidaAsync(senha, ct).ConfigureAwait(false))
            .WithMessage("Essa senha apareceu em vazamentos de dados; escolha outra.");
}
