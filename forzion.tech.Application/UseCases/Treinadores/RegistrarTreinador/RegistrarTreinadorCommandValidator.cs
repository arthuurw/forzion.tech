using FluentValidation;

namespace forzion.tech.Application.UseCases.Treinadores.RegistrarTreinador;

public class RegistrarTreinadorCommandValidator : AbstractValidator<RegistrarTreinadorCommand>
{
    public RegistrarTreinadorCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Senha).NotEmpty().MinimumLength(8).MaximumLength(72);
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(100);
    }
}
