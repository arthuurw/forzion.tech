using FluentValidation;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Alunos.RegistrarAluno;

public class RegistrarAlunoCommandValidator : AbstractValidator<RegistrarAlunoCommand>
{
    public RegistrarAlunoCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Senha)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(72)
            .Matches(@"(?=.*[a-z])").WithMessage("Senha deve conter ao menos uma letra minúscula.")
            .Matches(@"(?=.*[A-Z])").WithMessage("Senha deve conter ao menos uma letra maiúscula.")
            .Matches(@"(?=.*\d)").WithMessage("Senha deve conter ao menos um número.");
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TreinadorId).NotEmpty();
        RuleFor(x => x.PacoteId).NotEmpty();
        RuleFor(x => x.Telefone).MaximumLength(20).When(x => x.Telefone is not null);
        RuleFor(x => x.DiasDisponiveis).InclusiveBetween(1, 7).When(x => x.DiasDisponiveis.HasValue);
        RuleFor(x => x.TempoDisponivelMinutos)
            .Must(v => Enum.IsDefined((TempoDisponivel)v!.Value))
            .When(x => x.TempoDisponivelMinutos.HasValue)
            .WithMessage("Tempo disponível inválido. Valores aceitos: 30, 45, 60, 90, 120 (minutos)");
        RuleFor(x => x.FocoTreino).MaximumLength(200).When(x => x.FocoTreino is not null);
        RuleFor(x => x.LimitacoesFisicas).MaximumLength(500).When(x => x.LimitacoesFisicas is not null);
        RuleFor(x => x.Doencas).MaximumLength(500).When(x => x.Doencas is not null);
        RuleFor(x => x.ObservacoesAdicionais).MaximumLength(1000).When(x => x.ObservacoesAdicionais is not null);
    }
}
