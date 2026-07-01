using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Validation;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Alunos.RegistrarAluno;

public class RegistrarAlunoCommandValidator : AbstractValidator<RegistrarAlunoCommand>
{
    public RegistrarAlunoCommandValidator(IPwnedPasswordsService pwnedPasswords)
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(EmailErrors.Obrigatorio.Message)
            .EmailAddress().WithMessage(EmailErrors.Invalido.Message)
            .MaximumLength(256).WithMessage(EmailErrors.MuitoLongo.Message);
        RuleFor(x => x.Senha).SenhaForte().SenhaNaoComprometida(pwnedPasswords);
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage(NomeErrors.Obrigatorio.Message)
            .MaximumLength(100).WithMessage(NomeErrors.MuitoLongo.Message);
        RuleFor(x => x.TreinadorId).NotEmpty().WithMessage("O treinador é obrigatório.");
        RuleFor(x => x.PacoteId).NotEmpty().WithMessage("O pacote é obrigatório.");
        RuleFor(x => x.Telefone).MaximumLength(20).WithMessage(TelefoneErrors.MuitoLongo.Message).When(x => x.Telefone is not null);
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
