using FluentValidation;

namespace forzion.tech.Application.UseCases.Planos.AtualizarPlanoTreinador;

public class AtualizarPlanoTreinadorCommandValidator : AbstractValidator<AtualizarPlanoTreinadorCommand>
{
    public AtualizarPlanoTreinadorCommandValidator()
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.")
            .When(x => x.Nome is not null);

        RuleFor(x => x.MaxAlunos)
            .GreaterThan(0).WithMessage("O limite de alunos deve ser maior que zero.")
            .When(x => x.MaxAlunos is not null);

        RuleFor(x => x.Preco)
            .GreaterThanOrEqualTo(0).WithMessage("O preço deve ser maior ou igual a zero.")
            .When(x => x.Preco is not null);
    }
}
