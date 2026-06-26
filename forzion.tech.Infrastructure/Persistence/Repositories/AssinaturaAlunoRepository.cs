using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class AssinaturaAlunoRepository(AppDbContext context) : IAssinaturaAlunoRepository
{
    public async Task<AssinaturaAluno?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.AssinaturaAlunos
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<AssinaturaAluno?> ObterPorVinculoIdAsync(Guid vinculoId, CancellationToken cancellationToken = default) =>
        await context.AssinaturaAlunos
            .FirstOrDefaultAsync(a => a.VinculoId == vinculoId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<AssinaturaAluno?> ObterAtualPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default) =>
        await context.AssinaturaAlunos
            .AsNoTracking()
            .Where(a => a.AlunoId == alunoId && a.Status != AssinaturaAlunoStatus.Cancelada)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> AlunoEstaInadimplentePorContaIdAsync(Guid contaId, CancellationToken cancellationToken = default)
    {
        var status = await (
            from a in context.Alunos
            where a.ContaId == contaId
            join asg in context.AssinaturaAlunos on a.Id equals asg.AlunoId
            where asg.Status != AssinaturaAlunoStatus.Cancelada
            orderby asg.CreatedAt descending
            select (AssinaturaAlunoStatus?)asg.Status)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return status == AssinaturaAlunoStatus.Inadimplente;
    }

    // Keyset por Id (uuid é ordenável no Postgres): renovação bem-sucedida tira a linha do filtro,
    // então offset/Skip pularia não-processados. Cursor avança mesmo em falha → falha não reprocessa
    // no mesmo run (próximo cron pega), evitando loop infinito.
    public async Task<IReadOnlyList<AssinaturaAluno>> ListarParaRenovarAsync(DateTime ate, Guid? aposId, int limite, CancellationToken cancellationToken = default) =>
        await context.AssinaturaAlunos
            .AsNoTracking()
            .Where(a => a.Status == AssinaturaAlunoStatus.Ativa && a.DataProximaCobranca <= ate
                        && (aposId == null || a.Id > aposId))
            .OrderBy(a => a.Id)
            .Take(limite)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<AssinaturaAluno>> ListarParaPreAvisoAsync(DateTime inicio, DateTime fim, CancellationToken cancellationToken = default) =>
        await context.AssinaturaAlunos
            .AsNoTracking()
            .Where(a => a.Status == AssinaturaAlunoStatus.Ativa
                        && a.DataProximaCobranca >= inicio && a.DataProximaCobranca < fim)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<AssinaturaAluno>> ListarPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default) =>
        await context.AssinaturaAlunos
            .AsNoTracking()
            .Where(a => a.AlunoId == alunoId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    // Tracked (sem AsNoTracking): o caller cancela cada assinatura e persiste via UnitOfWork.
    public async Task<IReadOnlyList<AssinaturaAluno>> ListarNaoCanceladasPorTreinadorAsync(Guid treinadorId, CancellationToken cancellationToken = default) =>
        await context.AssinaturaAlunos
            .Where(a => a.TreinadorId == treinadorId && a.Status != AssinaturaAlunoStatus.Cancelada)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarAsync(AssinaturaAluno assinatura, CancellationToken cancellationToken = default) =>
        await context.AssinaturaAlunos.AddAsync(assinatura, cancellationToken).ConfigureAwait(false);

    public async Task<int> ContarPorStatusAsync(AssinaturaAlunoStatus status, CancellationToken cancellationToken = default) =>
        await context.AssinaturaAlunos
            .CountAsync(a => a.Status == status, cancellationToken)
            .ConfigureAwait(false);

    public async Task ExcluirPorAlunoIdAsync(Guid alunoId, CancellationToken cancellationToken = default) =>
        await context.AssinaturaAlunos
            .Where(a => a.AlunoId == alunoId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
}
