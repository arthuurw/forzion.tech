using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Nfse.ListarNotasFiscaisTreinador;

public record NotaFiscalResumoResponse(
    Guid Id,
    TipoNotaFiscal Tipo,
    NotaFiscalStatus Status,
    decimal Valor,
    DateOnly? CompetenciaInicio,
    DateOnly? CompetenciaFim,
    string? NumeroNfse,
    DateTime? DataEmissao,
    bool TemDanfse,
    DateTime CriadoEm);

public record ListarNotasFiscaisResponse(IReadOnlyList<NotaFiscalResumoResponse> Itens, Guid? ProximoCursor);

public class ListarNotasFiscaisTreinadorHandler(INotaFiscalRepository notaFiscalRepository)
{
    public const int LimiteMaximo = 100;

    public virtual async Task<ListarNotasFiscaisResponse> HandleAsync(
        Guid treinadorId,
        Guid? aposId,
        int limite,
        CancellationToken cancellationToken = default)
    {
        var limiteFinal = limite < 1 ? 20 : Math.Min(limite, LimiteMaximo);
        var notas = await notaFiscalRepository.ListarPorTreinadorAsync(treinadorId, aposId, limiteFinal, cancellationToken).ConfigureAwait(false);
        var itens = notas.Select(Map).ToList();
        var proximoCursor = itens.Count == limiteFinal ? itens[^1].Id : (Guid?)null;
        return new ListarNotasFiscaisResponse(itens, proximoCursor);
    }

    internal static NotaFiscalResumoResponse Map(NotaFiscal n) =>
        new(n.Id, n.Tipo, n.Status, n.Valor, n.CompetenciaInicio, n.CompetenciaFim,
            n.NumeroNfse, n.DataEmissao, !string.IsNullOrWhiteSpace(n.DanfseRef), n.CreatedAt);
}
