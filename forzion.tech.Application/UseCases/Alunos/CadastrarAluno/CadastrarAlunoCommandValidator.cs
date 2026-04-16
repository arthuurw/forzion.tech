using FluentValidation;

namespace forzion.tech.Application.UseCases.Alunos.CadastrarAluno;

public class CadastrarAlunoCommandValidator : AbstractValidator<CadastrarAlunoCommand>
{
    public CadastrarAlunoCommandValidator()
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("E-mail inválido.")
            .MaximumLength(256).WithMessage("O e-mail deve ter no máximo 256 caracteres.")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Telefone)
            .MaximumLength(20).WithMessage("O telefone deve ter no máximo 20 caracteres.")
            .When(x => !string.IsNullOrEmpty(x.Telefone));
    }
}
