using FluentValidation;

namespace forzion.tech.Application.UseCases.Admin.GruposMusculares.CriarGrupoMuscular;

public class CriarGrupoMuscularCommandValidator : AbstractValidator<CriarGrupoMuscularCommand>
{
    public CriarGrupoMuscularCommandValidator()
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.");
    }
}
