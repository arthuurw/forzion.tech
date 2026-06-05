using FluentValidation;

namespace forzion.tech.Application.UseCases.Treinadores.RegistrarTreinador;

public class RegistrarTreinadorCommandValidator : AbstractValidator<RegistrarTreinadorCommand>
{
    public RegistrarTreinadorCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Senha)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(72)
            .Matches(@"(?=.*[a-z])").WithMessage("Senha deve conter ao menos uma letra minúscula.")
            .Matches(@"(?=.*[A-Z])").WithMessage("Senha deve conter ao menos uma letra maiúscula.")
            .Matches(@"(?=.*\d)").WithMessage("Senha deve conter ao menos um número.");
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PlanoPlataformaId).NotEmpty();
        RuleFor(x => x.ModoPagamentoAluno).IsInEnum();
        RuleFor(x => x.Telefone).MaximumLength(20).When(x => x.Telefone is not null);
    }
}
