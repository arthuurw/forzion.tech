using FluentValidation;

namespace forzion.tech.Application.UseCases.Treinos.AdicionarExercicio;

public class AdicionarExercicioCommandValidator : AbstractValidator<AdicionarExercicioCommand>
{
    public AdicionarExercicioCommandValidator()
    {
        RuleFor(x => x.ExercicioId)
            .NotEmpty().WithMessage("O ID do exercício é obrigatório.");

        RuleFor(x => x.Series)
            .NotEmpty().WithMessage("Adicione ao menos um grupo de séries.");

        RuleForEach(x => x.Series).ChildRules(serie =>
        {
            serie.RuleFor(s => s.Quantidade)
                .GreaterThan(0).WithMessage("A quantidade de séries deve ser maior que zero.");

            serie.RuleFor(s => s.RepeticoesMin)
                .GreaterThan(0).WithMessage("O número mínimo de repetições deve ser maior que zero.");

            serie.RuleFor(s => s.RepeticoesMax)
                .GreaterThanOrEqualTo(s => s.RepeticoesMin)
                .WithMessage("O máximo de repetições não pode ser menor que o mínimo.")
                .When(s => s.RepeticoesMax.HasValue);

            serie.RuleFor(s => s.Descricao)
                .MaximumLength(100).WithMessage("A descrição deve ter no máximo 100 caracteres.")
                .When(s => s.Descricao is not null);

            serie.RuleFor(s => s.Carga)
                .GreaterThanOrEqualTo(0).WithMessage("A carga não pode ser negativa.")
                .When(s => s.Carga.HasValue);

            serie.RuleFor(s => s.Descanso)
                .GreaterThanOrEqualTo(0).WithMessage("O descanso não pode ser negativo.")
                .When(s => s.Descanso.HasValue);
        });
    }
}
