using FluentValidation;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Alunos.AtualizarAnamneseAluno;

public class AtualizarAnamneseAlunoCommandValidator : AbstractValidator<AtualizarAnamneseAlunoCommand>
{
    public AtualizarAnamneseAlunoCommandValidator()
    {
        RuleFor(x => x.AlunoId).NotEmpty();
        RuleFor(x => x.DiasDisponiveis).InclusiveBetween(1, 7).WithMessage("Os dias disponíveis devem estar entre 1 e 7.").When(x => x.DiasDisponiveis.HasValue);
        RuleFor(x => x.TempoDisponivelMinutos)
            .Must(v => Enum.IsDefined((TempoDisponivel)v!.Value))
            .When(x => x.TempoDisponivelMinutos.HasValue)
            .WithMessage("Tempo disponível inválido. Valores aceitos: 30, 45, 60, 90, 120 (minutos)");
        RuleFor(x => x.FocoTreino).MaximumLength(200).When(x => x.FocoTreino is not null);
        RuleFor(x => x.LimitacoesFisicas).MaximumLength(500).When(x => x.LimitacoesFisicas is not null);
        RuleFor(x => x.Doencas).MaximumLength(500).When(x => x.Doencas is not null);
        RuleFor(x => x.ObservacoesAdicionais).MaximumLength(1000).When(x => x.ObservacoesAdicionais is not null);
        RuleFor(x => x.ConsentimentoDadosSaude)
            .Equal(true)
            .When(x => x.ColetaDadosSaude)
            .WithErrorCode("consentimento_saude_obrigatorio")
            .WithMessage("Consentimento explícito para tratamento de dados de saúde é obrigatório (LGPD art. 11).");
    }
}
