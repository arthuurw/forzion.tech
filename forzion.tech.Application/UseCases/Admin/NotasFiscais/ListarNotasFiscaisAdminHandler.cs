using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Admin.NotasFiscais;

public record NotaFiscalAdminResponse(
    Guid Id,
    Guid TreinadorId,
    TipoNotaFiscal Tipo,
    NotaFiscalStatus Status,
    decimal Valor,
    DateOnly? CompetenciaInicio,
    DateOnly? CompetenciaFim,
    string? NumeroNfse,
    string? ChaveAcesso,
    DateTime? DataEmissao,
    string? CodigoErro,
    string? MotivoErro,
    DateTime CriadoEm);

public record ListarNotasFiscaisAdminResponse(IReadOnlyList<NotaFiscalAdminResponse> Itens, Guid? ProximoCursor);

public class ListarNotasFiscaisAdminHandler(INotaFiscalRepository notaFiscalRepository)
{
    public const int LimiteMaximo = 100;

    public virtual async Task<ListarNotasFiscaisAdminResponse> HandleAsync(
        NotaFiscalStatus? status,
        Guid? treinadorId,
        Guid? aposId,
        int limite,
        CancellationToken cancellationToken = default)
    {
        var limiteFinal = limite < 1 ? 20 : Math.Min(limite, LimiteMaximo);
        var notas = await notaFiscalRepository.ListarAdminAsync(status, treinadorId, aposId, limiteFinal, cancellationToken).ConfigureAwait(false);
        var itens = notas.Select(Map).ToList();
        var proximoCursor = itens.Count == limiteFinal ? itens[^1].Id : (Guid?)null;
        return new ListarNotasFiscaisAdminResponse(itens, proximoCursor);
    }

    private static NotaFiscalAdminResponse Map(NotaFiscal n) =>
        new(n.Id, n.TreinadorId, n.Tipo, n.Status, n.Valor, n.CompetenciaInicio, n.CompetenciaFim,
            n.NumeroNfse, n.ChaveAcesso, n.DataEmissao, n.CodigoErro, n.MotivoErro, n.CreatedAt);
}
