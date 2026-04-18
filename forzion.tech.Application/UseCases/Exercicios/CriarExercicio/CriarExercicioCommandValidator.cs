using FluentValidation;

namespace forzion.tech.Application.UseCases.Exercicios.CriarExercicio;

public class CriarExercicioCommandValidator : AbstractValidator<CriarExercicioCommand>
{
    public CriarExercicioCommandValidator()
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.");

        RuleFor(x => x.Descricao)
            .MaximumLength(500).WithMessage("A descrição deve ter no máximo 500 caracteres.")
            .When(x => !string.IsNullOrEmpty(x.Descricao));

        RuleFor(x => x.GrupoMuscular)
            .IsInEnum().WithMessage("Grupo muscular inválido.");
    }
}
