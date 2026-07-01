using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Validation;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Treinadores.RegistrarTreinador;

public class RegistrarTreinadorCommandValidator : AbstractValidator<RegistrarTreinadorCommand>
{
    public RegistrarTreinadorCommandValidator(IPwnedPasswordsService pwnedPasswords)
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(EmailErrors.Obrigatorio.Message)
            .EmailAddress().WithMessage(EmailErrors.Invalido.Message)
            .MaximumLength(256).WithMessage(EmailErrors.MuitoLongo.Message);
        RuleFor(x => x.Senha).SenhaForte().SenhaNaoComprometida(pwnedPasswords);
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage(NomeErrors.Obrigatorio.Message)
            .MaximumLength(100).WithMessage(NomeErrors.MuitoLongo.Message);
        RuleFor(x => x.PlanoPlataformaId).NotEmpty().WithMessage("O plano é obrigatório.");
        RuleFor(x => x.ModoPagamentoAluno).IsInEnum().WithMessage("Modo de pagamento inválido.");
        RuleFor(x => x.Telefone).MaximumLength(20).WithMessage(TelefoneErrors.MuitoLongo.Message).When(x => x.Telefone is not null);
    }
}
