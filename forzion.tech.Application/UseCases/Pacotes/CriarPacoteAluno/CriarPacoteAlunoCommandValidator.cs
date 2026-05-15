using FluentValidation;

namespace forzion.tech.Application.UseCases.Pacotes.CriarPacoteAluno;

public class CriarPacoteAlunoCommandValidator : AbstractValidator<CriarPacoteAlunoCommand>
{
    public CriarPacoteAlunoCommandValidator()
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.");

        RuleFor(x => x.Preco)
            .GreaterThanOrEqualTo(0).WithMessage("O preço deve ser maior ou igual a zero.");

        RuleFor(x => x.Descricao)
            .MaximumLength(500).WithMessage("A descrição deve ter no máximo 500 caracteres.")
            .When(x => !string.IsNullOrEmpty(x.Descricao));
    }
}
