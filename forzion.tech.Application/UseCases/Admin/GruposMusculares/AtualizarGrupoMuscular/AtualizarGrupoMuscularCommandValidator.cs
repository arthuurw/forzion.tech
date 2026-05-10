using FluentValidation;

namespace forzion.tech.Application.UseCases.Admin.GruposMusculares.AtualizarGrupoMuscular;

public class AtualizarGrupoMuscularCommandValidator : AbstractValidator<AtualizarGrupoMuscularCommand>
{
    public AtualizarGrupoMuscularCommandValidator()
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.");
    }
}
