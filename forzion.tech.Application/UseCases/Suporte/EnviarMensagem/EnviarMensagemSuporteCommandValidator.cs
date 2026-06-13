using FluentValidation;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Suporte.EnviarMensagem;

public class EnviarMensagemSuporteCommandValidator : AbstractValidator<EnviarMensagemSuporteCommand>
{
    public EnviarMensagemSuporteCommandValidator()
    {
        RuleFor(x => x.Categoria)
            .NotEmpty().WithMessage("A categoria é obrigatória.")
            .IsEnumName(typeof(CategoriaSuporte), caseSensitive: false).WithMessage("A categoria informada é inválida.");

        RuleFor(x => x.Assunto)
            .NotEmpty().WithMessage("O assunto é obrigatório.")
            .MinimumLength(MensagemSuporte.AssuntoMinLength).MaximumLength(MensagemSuporte.AssuntoMaxLength)
            .WithMessage($"O assunto deve ter entre {MensagemSuporte.AssuntoMinLength} e {MensagemSuporte.AssuntoMaxLength} caracteres.");

        RuleFor(x => x.Descricao)
            .NotEmpty().WithMessage("A descrição é obrigatória.")
            .MinimumLength(MensagemSuporte.DescricaoMinLength).MaximumLength(MensagemSuporte.DescricaoMaxLength)
            .WithMessage($"A descrição deve ter entre {MensagemSuporte.DescricaoMinLength} e {MensagemSuporte.DescricaoMaxLength} caracteres.");
    }
}
