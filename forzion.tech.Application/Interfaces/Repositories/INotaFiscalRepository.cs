using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface INotaFiscalRepository
{
    Task AdicionarAsync(NotaFiscal notaFiscal, CancellationToken cancellationToken = default);
    Task<NotaFiscal?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NotaFiscal?> ObterPorPagamentoTreinadorAsync(Guid pagamentoTreinadorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotaFiscal>> ListarPorTreinadorAsync(Guid treinadorId, Guid? aposId, int limite, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotaFiscal>> ListarPorStatusAsync(NotaFiscalStatus status, Guid? aposId, int limite, CancellationToken cancellationToken = default);
    Task<bool> ExisteComissaoAsync(Guid treinadorId, DateOnly competenciaInicio, CancellationToken cancellationToken = default);
}
