using forzion.tech.Domain.Exceptions;

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

    public static ErrorLogEntry Criar(DateTime ocorridoEm, string nivel, string origem, string mensagem)
    {
        if (string.IsNullOrWhiteSpace(nivel))
            throw new DomainException("O nível é obrigatório.");
        if (string.IsNullOrWhiteSpace(origem))
            throw new DomainException("A origem é obrigatória.");

        var texto = mensagem ?? string.Empty;
        if (texto.Length > MensagemMaxLength)
            texto = texto[..MensagemMaxLength];

        return new ErrorLogEntry
        {
            Id = Guid.NewGuid(),
            OcorridoEm = ocorridoEm,
            Nivel = nivel.Trim(),
            Origem = origem.Trim(),
            Mensagem = texto,
            CreatedAt = DateTime.UtcNow
        };
    }
}
