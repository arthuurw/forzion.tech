using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

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

    public static Result<LogAprovacao> Registrar(
        TipoAcaoAprovacao tipoAcao,
        Guid realizadoPorId,
        Guid entidadeId,
        string entidadeTipo,
        DateTime agora,
        string? observacao = null)
    {
        if (realizadoPorId == Guid.Empty)
            return Result.Failure<LogAprovacao>(LogAprovacaoErrors.RealizadoPorIdInvalido);
        if (entidadeId == Guid.Empty)
            return Result.Failure<LogAprovacao>(LogAprovacaoErrors.EntidadeIdInvalido);
        if (string.IsNullOrWhiteSpace(entidadeTipo))
            return Result.Failure<LogAprovacao>(LogAprovacaoErrors.EntidadeTipoObrigatorio);
        if (observacao is not null && observacao.Length > 500)
            return Result.Failure<LogAprovacao>(LogAprovacaoErrors.ObservacaoMuitoLonga);

        return Result.Success(new LogAprovacao
        {
            Id = Guid.NewGuid(),
            TipoAcao = tipoAcao,
            RealizadoPorId = realizadoPorId,
            EntidadeId = entidadeId,
            EntidadeTipo = entidadeTipo,
            Observacao = observacao,
            CreatedAt = agora
        });
    }
}
