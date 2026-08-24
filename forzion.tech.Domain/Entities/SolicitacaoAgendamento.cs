using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class SolicitacaoAgendamento : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    public Guid Id { get; private set; }
    public Guid TreinadorId { get; private set; }
    public Guid PacoteId { get; private set; }
    public Guid LeadId { get; private set; }
    public string SlotId { get; private set; } = string.Empty;
    public DateTime InicioUtc { get; private set; }
    public DateTime FimUtc { get; private set; }
    public SolicitacaoAgendamentoStatus Status { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string ArgumentosHash { get; private set; } = string.Empty;
    public string? Motivo { get; private set; }
    public DateTime? DecididaEm { get; private set; }
    public Guid? DecididaPorId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private SolicitacaoAgendamento() { }

    public static Result<SolicitacaoAgendamento> Criar(
        Guid treinadorId,
        Guid pacoteId,
        Guid leadId,
        string slotId,
        DateTime inicioUtc,
        DateTime fimUtc,
        string idempotencyKey,
        string argumentosHash,
        DateTime agora)
    {
        if (treinadorId == Guid.Empty)
            return Result.Failure<SolicitacaoAgendamento>(SolicitacaoAgendamentoErrors.TreinadorIdInvalido);
        if (pacoteId == Guid.Empty)
            return Result.Failure<SolicitacaoAgendamento>(SolicitacaoAgendamentoErrors.PacoteIdInvalido);
        if (leadId == Guid.Empty)
            return Result.Failure<SolicitacaoAgendamento>(SolicitacaoAgendamentoErrors.LeadIdInvalido);
        if (string.IsNullOrWhiteSpace(slotId))
            return Result.Failure<SolicitacaoAgendamento>(SolicitacaoAgendamentoErrors.SlotIdObrigatorio);
        if (inicioUtc >= fimUtc)
            return Result.Failure<SolicitacaoAgendamento>(SolicitacaoAgendamentoErrors.IntervaloInvalido);
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Result.Failure<SolicitacaoAgendamento>(SolicitacaoAgendamentoErrors.IdempotencyKeyObrigatoria);
        if (idempotencyKey.Length > 200)
            return Result.Failure<SolicitacaoAgendamento>(SolicitacaoAgendamentoErrors.IdempotencyKeyMuitoLonga);

        var solicitacao = new SolicitacaoAgendamento
        {
            Id = Guid.NewGuid(),
            TreinadorId = treinadorId,
            PacoteId = pacoteId,
            LeadId = leadId,
            SlotId = slotId,
            InicioUtc = inicioUtc,
            FimUtc = fimUtc,
            Status = SolicitacaoAgendamentoStatus.PendenteAgente,
            IdempotencyKey = idempotencyKey,
            ArgumentosHash = argumentosHash,
            CreatedAt = agora
        };
        solicitacao._domainEvents.Add(new SolicitacaoAgendamentoCriadaEvent(solicitacao.Id, treinadorId, pacoteId, inicioUtc, agora));
        return Result.Success(solicitacao);
    }

    public Result Confirmar(Guid realizadoPorId, DateTime agora)
    {
        if (Status != SolicitacaoAgendamentoStatus.PendenteAgente)
            return Result.Failure(SolicitacaoAgendamentoErrors.TransicaoNaoSuportada);
        if (InicioUtc <= agora)
            return Result.Failure(SolicitacaoAgendamentoErrors.SlotJaIniciado);

        Status = SolicitacaoAgendamentoStatus.Confirmada;
        DecididaEm = agora;
        DecididaPorId = realizadoPorId;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result Recusar(Guid realizadoPorId, string? motivo, DateTime agora)
    {
        if (Status != SolicitacaoAgendamentoStatus.PendenteAgente)
            return Result.Failure(SolicitacaoAgendamentoErrors.TransicaoNaoSuportada);

        var motivoResult = NormalizarMotivo(motivo);
        if (motivoResult.IsFailure)
            return Result.Failure(motivoResult.Error!);

        Status = SolicitacaoAgendamentoStatus.Recusada;
        Motivo = motivoResult.Value;
        DecididaEm = agora;
        DecididaPorId = realizadoPorId;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result Cancelar(Guid realizadoPorId, string? motivo, DateTime agora)
    {
        if (Status != SolicitacaoAgendamentoStatus.Confirmada)
            return Result.Failure(SolicitacaoAgendamentoErrors.TransicaoNaoSuportada);

        var motivoResult = NormalizarMotivo(motivo);
        if (motivoResult.IsFailure)
            return Result.Failure(motivoResult.Error!);

        Status = SolicitacaoAgendamentoStatus.Cancelada;
        Motivo = motivoResult.Value;
        DecididaEm = agora;
        DecididaPorId = realizadoPorId;
        UpdatedAt = agora;
        return Result.Success();
    }

    private static Result<string?> NormalizarMotivo(string? motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            return Result.Success<string?>(null);

        var normalizado = motivo.Trim();
        if (normalizado.Length > 500)
            return Result.Failure<string?>(SolicitacaoAgendamentoErrors.MotivoMuitoLongo);

        return Result.Success<string?>(normalizado);
    }
}
