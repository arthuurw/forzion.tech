using FluentValidation;

namespace forzion.tech.Application.UseCases.Planos.CriarPlanoPlataforma;

public class CriarPlanoPlataformaCommandValidator : AbstractValidator<CriarPlanoPlataformaCommand>
{
    public CriarPlanoPlataformaCommandValidator()
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.");

        RuleFor(x => x.MaxAlunos)
            .GreaterThan(0).WithMessage("O limite de alunos deve ser maior que zero.");

        RuleFor(x => x.Preco)
            .GreaterThanOrEqualTo(0).WithMessage("O preço deve ser maior ou igual a zero.");
    }
}
