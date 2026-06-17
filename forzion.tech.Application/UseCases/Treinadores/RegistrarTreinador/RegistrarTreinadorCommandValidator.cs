using FluentValidation;
using forzion.tech.Application.Validation;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Treinadores.RegistrarTreinador;

public class RegistrarTreinadorCommandValidator : AbstractValidator<RegistrarTreinadorCommand>
{
    public RegistrarTreinadorCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(EmailErrors.Obrigatorio.Message)
            .EmailAddress().WithMessage(EmailErrors.Invalido.Message)
            .MaximumLength(256).WithMessage(EmailErrors.MuitoLongo.Message);
        RuleFor(x => x.Senha).SenhaForte();
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.");
        RuleFor(x => x.PlanoPlataformaId).NotEmpty().WithMessage("O plano é obrigatório.");
        RuleFor(x => x.ModoPagamentoAluno).IsInEnum().WithMessage("Modo de pagamento inválido.");
        RuleFor(x => x.Telefone).MaximumLength(20).WithMessage("O telefone deve ter no máximo 20 caracteres.").When(x => x.Telefone is not null);
    }
}
