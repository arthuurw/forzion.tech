using FluentValidation;
using forzion.tech.Application.Validation;

namespace forzion.tech.Application.UseCases.Treinadores.RegistrarTreinador;

public class RegistrarTreinadorCommandValidator : AbstractValidator<RegistrarTreinadorCommand>
{
    public RegistrarTreinadorCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Senha).SenhaForte();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PlanoPlataformaId).NotEmpty();
        RuleFor(x => x.ModoPagamentoAluno).IsInEnum();
        RuleFor(x => x.Telefone).MaximumLength(20).When(x => x.Telefone is not null);
    }
}
