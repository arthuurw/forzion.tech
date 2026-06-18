namespace forzion.tech.Application.Outbox;

public sealed record CancelarNfsePayload(Guid NotaFiscalId, string Motivo);
