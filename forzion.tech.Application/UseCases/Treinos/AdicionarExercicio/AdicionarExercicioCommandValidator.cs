using FluentValidation;

namespace forzion.tech.Application.UseCases.Treinos.AdicionarExercicio;

public class AdicionarExercicioCommandValidator : AbstractValidator<AdicionarExercicioCommand>
{
    public AdicionarExercicioCommandValidator()
    {
        RuleFor(x => x.ExercicioId)
            .NotEmpty().WithMessage("O ID do exercício é obrigatório.");

        RuleFor(x => x.Series)
            .GreaterThan(0).WithMessage("O número de séries deve ser maior que zero.");

        RuleFor(x => x.Repeticoes)
            .GreaterThan(0).WithMessage("O número de repetições deve ser maior que zero.");

        RuleFor(x => x.Carga)
            .GreaterThanOrEqualTo(0).WithMessage("A carga não pode ser negativa.")
            .When(x => x.Carga.HasValue);

        RuleFor(x => x.Descanso)
            .GreaterThanOrEqualTo(0).WithMessage("O descanso não pode ser negativo.")
            .When(x => x.Descanso.HasValue);
    }
}
