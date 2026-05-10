using FluentValidation;

namespace forzion.tech.Application.UseCases.Treinos.CriarTreino;

public class CriarTreinoCommandValidator : AbstractValidator<CriarTreinoCommand>
{
    public CriarTreinoCommandValidator()
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("O nome do treino é obrigatório.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.");

        RuleFor(x => x.Objetivo)
            .IsInEnum().WithMessage("Objetivo de treino inválido.");

    }
}
