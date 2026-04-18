using FluentValidation;

namespace forzion.tech.Application.UseCases.Alunos.RegistrarAluno;

public class RegistrarAlunoCommandValidator : AbstractValidator<RegistrarAlunoCommand>
{
    public RegistrarAlunoCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Senha).NotEmpty().MinimumLength(8).MaximumLength(72);
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TreinadorId).NotEmpty();
        RuleFor(x => x.Telefone).MaximumLength(20).When(x => x.Telefone is not null);
    }
}
