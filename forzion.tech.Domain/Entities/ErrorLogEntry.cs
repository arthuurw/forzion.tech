using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class ErrorLogEntry
{
    public const int MensagemMaxLength = 4000;

    public Guid Id { get; private set; }
    public DateTime OcorridoEm { get; private set; }
    public string Nivel { get; private set; } = string.Empty;
    public string Origem { get; private set; } = string.Empty;
    public string Mensagem { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private ErrorLogEntry() { }

    public static Result<ErrorLogEntry> Criar(DateTime ocorridoEm, string nivel, string origem, string mensagem, DateTime agora)
    {
        if (string.IsNullOrWhiteSpace(nivel))
            return Result.Failure<ErrorLogEntry>(LogAprovacaoErrors.NivelObrigatorio);
        if (string.IsNullOrWhiteSpace(origem))
            return Result.Failure<ErrorLogEntry>(LogAprovacaoErrors.OrigemObrigatoria);

        var texto = mensagem ?? string.Empty;
        if (texto.Length > MensagemMaxLength)
            texto = texto[..MensagemMaxLength];

        return Result.Success(new ErrorLogEntry
        {
            Id = Guid.NewGuid(),
            OcorridoEm = ocorridoEm,
            Nivel = nivel.Trim(),
            Origem = origem.Trim(),
            Mensagem = texto,
            CreatedAt = agora
        });
    }
}
