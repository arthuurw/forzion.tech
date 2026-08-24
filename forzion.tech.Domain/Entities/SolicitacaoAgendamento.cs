using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class SolicitacaoAgendamento
{
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
    public string? Motivo { get; }
    public DateTime? DecididaEm { get; }
    public Guid? DecididaPorId { get; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; }

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

        return Result.Success(new SolicitacaoAgendamento
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
        });
    }
}
