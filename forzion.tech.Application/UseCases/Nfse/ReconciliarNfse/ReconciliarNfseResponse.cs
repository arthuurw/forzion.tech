namespace forzion.tech.Application.UseCases.Nfse.ReconciliarNfse;

public sealed record ReconciliarNfseResponse(
    int Consultadas,
    int Atualizadas,
    int SemAlteracao,
    int Erros);
