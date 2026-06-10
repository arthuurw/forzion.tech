namespace forzion.tech.Application.Outbox;

public sealed record EvidenciaDisputaPayload(
    string DisputeId,
    string? Email,
    DateTime? DataAtivacao,
    DateTime? DataPagamento,
    Guid PagamentoId);
