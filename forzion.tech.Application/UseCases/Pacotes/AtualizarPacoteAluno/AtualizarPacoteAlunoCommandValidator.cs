using FluentValidation;

namespace forzion.tech.Application.UseCases.Pacotes.AtualizarPacoteAluno;

public class AtualizarPacoteAlunoCommandValidator : AbstractValidator<AtualizarPacoteAlunoCommand>
{
    public AtualizarPacoteAlunoCommandValidator()
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.")
            .When(x => x.Nome is not null);

        RuleFor(x => x.Preco)
            .GreaterThanOrEqualTo(0).WithMessage("O preço deve ser maior ou igual a zero.")
            .When(x => x.Preco is not null);

        RuleFor(x => x.Descricao)
            .MaximumLength(500).WithMessage("A descrição deve ter no máximo 500 caracteres.")
            .When(x => !string.IsNullOrEmpty(x.Descricao));
    }
}
