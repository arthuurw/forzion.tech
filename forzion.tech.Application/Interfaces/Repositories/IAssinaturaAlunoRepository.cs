using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface IAssinaturaAlunoRepository
{
    Task<AssinaturaAluno?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AssinaturaAluno?> ObterPorVinculoIdAsync(Guid vinculoId, CancellationToken cancellationToken = default);
    Task<AssinaturaAluno?> ObterAtualPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssinaturaAluno>> ListarParaRenovarAsync(DateTime ate, Guid? aposId, int limite, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssinaturaAluno>> ListarParaPreAvisoAsync(DateTime inicio, DateTime fim, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssinaturaAluno>> ListarPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssinaturaAluno>> ListarNaoCanceladasPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default);
    Task AdicionarAsync(AssinaturaAluno assinatura, CancellationToken cancellationToken = default);
    Task<int> ContarPorStatusAsync(AssinaturaAlunoStatus status, CancellationToken cancellationToken = default);
}
