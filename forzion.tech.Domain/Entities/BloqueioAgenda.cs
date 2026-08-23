using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class BloqueioAgenda
{
    public Guid Id { get; private set; }
    public Guid TreinadorId { get; private set; }
    public TipoBloqueio Tipo { get; private set; }
    public DateTime? InicioUtc { get; private set; }
    public DateTime? FimUtc { get; private set; }
    public int? DiaSemana { get; private set; }
    public TimeOnly? HoraInicio { get; private set; }
    public TimeOnly? HoraFim { get; private set; }
    public string? Motivo { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private BloqueioAgenda() { }

    public static Result<BloqueioAgenda> CriarPontual(Guid treinadorId, DateTime inicioUtc, DateTime fimUtc, string? motivo, DateTime agora)
    {
        if (treinadorId == Guid.Empty)
            return Result.Failure<BloqueioAgenda>(BloqueioAgendaErrors.TreinadorIdInvalido);
        if (inicioUtc >= fimUtc)
            return Result.Failure<BloqueioAgenda>(BloqueioAgendaErrors.IntervaloPontualInvalido);

        var motivoResult = NormalizarMotivo(motivo);
        if (motivoResult.IsFailure)
            return Result.Failure<BloqueioAgenda>(motivoResult.Error!);

        return Result.Success(new BloqueioAgenda
        {
            Id = Guid.NewGuid(),
            TreinadorId = treinadorId,
            Tipo = TipoBloqueio.Pontual,
            InicioUtc = inicioUtc,
            FimUtc = fimUtc,
            Motivo = motivoResult.Value,
            CreatedAt = agora
        });
    }

    public static Result<BloqueioAgenda> CriarRecorrente(Guid treinadorId, int diaSemana, TimeOnly horaInicio, TimeOnly horaFim, string? motivo, DateTime agora)
    {
        if (treinadorId == Guid.Empty)
            return Result.Failure<BloqueioAgenda>(BloqueioAgendaErrors.TreinadorIdInvalido);
        if (diaSemana is < 0 or > 6)
            return Result.Failure<BloqueioAgenda>(BloqueioAgendaErrors.DiaSemanaInvalido);
        if (horaInicio >= horaFim)
            return Result.Failure<BloqueioAgenda>(BloqueioAgendaErrors.IntervaloRecorrenteInvalido);

        var motivoResult = NormalizarMotivo(motivo);
        if (motivoResult.IsFailure)
            return Result.Failure<BloqueioAgenda>(motivoResult.Error!);

        return Result.Success(new BloqueioAgenda
        {
            Id = Guid.NewGuid(),
            TreinadorId = treinadorId,
            Tipo = TipoBloqueio.RecorrenteSemanal,
            DiaSemana = diaSemana,
            HoraInicio = horaInicio,
            HoraFim = horaFim,
            Motivo = motivoResult.Value,
            CreatedAt = agora
        });
    }

    private static Result<string?> NormalizarMotivo(string? motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            return Result.Success<string?>(null);

        var normalizado = motivo.Trim();
        if (normalizado.Length > 200)
            return Result.Failure<string?>(BloqueioAgendaErrors.MotivoMuitoLongo);

        return Result.Success<string?>(normalizado);
    }
}
