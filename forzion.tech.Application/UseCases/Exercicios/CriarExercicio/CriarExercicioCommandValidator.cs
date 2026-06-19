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

        RuleFor(x => x.ComoExecutar)
            .MaximumLength(2000).WithMessage("As instruções de execução devem ter no máximo 2000 caracteres.")
            .When(x => !string.IsNullOrEmpty(x.ComoExecutar));

        RuleFor(x => x.VideoUrl)
            .MaximumLength(256).WithMessage("O link do vídeo é muito longo.")
            .When(x => !string.IsNullOrEmpty(x.VideoUrl));

        RuleFor(x => x.GrupoMuscularId)
            .NotEmpty().WithMessage("O grupo muscular é obrigatório.");
    }
}
