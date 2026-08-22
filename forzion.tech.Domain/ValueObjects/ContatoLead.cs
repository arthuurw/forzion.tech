using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.ValueObjects;

public sealed record ContatoLead
{
    public TipoContatoLead Tipo { get; }
    public string Valor { get; }

    private ContatoLead(TipoContatoLead tipo, string valor)
    {
        Tipo = tipo;
        Valor = valor;
    }

    public static Result<ContatoLead> Criar(TipoContatoLead tipo, string valor)
    {
        if (!string.IsNullOrEmpty(valor) && valor.Length > 320)
            return Result.Failure<ContatoLead>(ContatoLeadErrors.ValorMuitoLongo);

        if (tipo == TipoContatoLead.Email)
        {
            var emailResult = Email.Criar(valor);
            return emailResult.IsFailure
                ? Result.Failure<ContatoLead>(emailResult.Error!)
                : Result.Success(new ContatoLead(tipo, emailResult.Value.Value));
        }

        var normalizado = PhoneNumberNormalizer.Normalizar(valor);
        return normalizado is null
            ? Result.Failure<ContatoLead>(ContatoLeadErrors.TelefoneInvalido)
            : Result.Success(new ContatoLead(tipo, normalizado));
    }
}
