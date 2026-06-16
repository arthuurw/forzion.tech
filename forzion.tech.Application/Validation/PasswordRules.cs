using FluentValidation;

namespace forzion.tech.Application.Validation;

public static class PasswordRules
{
    public static IRuleBuilderOptions<T, string> SenhaForte<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("A senha é obrigatória.")
            .MinimumLength(8).WithMessage("A senha deve ter pelo menos 8 caracteres.")
            .MaximumLength(72).WithMessage("A senha deve ter no máximo 72 caracteres.")
            .Matches("[a-z]").WithMessage("A senha deve conter pelo menos uma letra minúscula.")
            .Matches("[A-Z]").WithMessage("A senha deve conter pelo menos uma letra maiúscula.")
            .Matches("[0-9]").WithMessage("A senha deve conter pelo menos um dígito.");
}
