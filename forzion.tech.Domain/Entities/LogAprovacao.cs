using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Domain.Entities;

public class LogAprovacao
{
    public Guid Id { get; private set; }
    public TipoAcaoAprovacao TipoAcao { get; private set; }
    public Guid RealizadoPorId { get; private set; }
    public Guid EntidadeId { get; private set; }
    public string EntidadeTipo { get; private set; } = string.Empty;
    public string? Observacao { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private LogAprovacao() { }

    public static LogAprovacao Registrar(
        TipoAcaoAprovacao tipoAcao,
        Guid realizadoPorId,
        Guid entidadeId,
        string entidadeTipo,
        string? observacao = null)
    {
        if (realizadoPorId == Guid.Empty)
            throw new DomainException("O identificador de quem realizou a ação é inválido.");
        if (entidadeId == Guid.Empty)
            throw new DomainException("O identificador da entidade é inválido.");
        if (string.IsNullOrWhiteSpace(entidadeTipo))
            throw new DomainException("O tipo da entidade é obrigatório.");
        if (observacao is not null && observacao.Length > 500)
            throw new DomainException("A observação deve ter no máximo 500 caracteres.");

        return new LogAprovacao
        {
            Id = Guid.NewGuid(),
            TipoAcao = tipoAcao,
            RealizadoPorId = realizadoPorId,
            EntidadeId = entidadeId,
            EntidadeTipo = entidadeTipo,
            Observacao = observacao,
            CreatedAt = DateTime.UtcNow
        };
    }
}
