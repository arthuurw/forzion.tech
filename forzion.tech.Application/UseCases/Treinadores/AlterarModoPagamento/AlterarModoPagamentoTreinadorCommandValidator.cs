using FluentValidation;

namespace forzion.tech.Application.UseCases.Treinadores.AlterarModoPagamento;

public class AlterarModoPagamentoTreinadorCommandValidator : AbstractValidator<AlterarModoPagamentoTreinadorCommand>
{
    public AlterarModoPagamentoTreinadorCommandValidator()
    {
        RuleFor(x => x.NovoModo).IsInEnum().WithMessage("Modo de pagamento inválido.");
    }
}
