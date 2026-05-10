using FluentValidation;

namespace forzion.tech.Application.UseCases.Conta.AtualizarPerfil;

public class AtualizarPerfilCommandValidator : AbstractValidator<AtualizarPerfilCommand>
{
    public AtualizarPerfilCommandValidator()
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.");
    }
}
