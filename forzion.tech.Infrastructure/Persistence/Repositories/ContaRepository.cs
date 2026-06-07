using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class ContaRepository(AppDbContext context) : IContaRepository
{
    private readonly AppDbContext _context = context;

    public async Task<Conta?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Contas
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<Conta?> ObterPorEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var emailVo = Email.FromDatabase(email);
        return await _context.Contas
            .FirstOrDefaultAsync(c => c.Email == emailVo, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AdicionarAsync(Conta conta, CancellationToken cancellationToken = default) =>
        await _context.Contas.AddAsync(conta, cancellationToken).ConfigureAwait(false);

    public async Task<int> ContarCriadasDesdeAsync(DateTime desde, CancellationToken cancellationToken = default) =>
        await _context.Contas
            .CountAsync(c => c.CreatedAt >= desde, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Guid>> ListarElegivelPurgaLgpdAsync(DateTime threshold, CancellationToken cancellationToken = default)
    {
        // Conta elegível: não anonimizada, teve ao menos uma assinatura e TODAS estão
        // Canceladas com DataCancelamento anterior ao threshold (retenção fiscal 5 anos).
        var contasAluno =
            from a in _context.Alunos.AsNoTracking()
            join s in _context.AssinaturaAlunos.AsNoTracking() on a.Id equals s.AlunoId
            select new { a.ContaId, s.Status, s.DataCancelamento };

        var contasTreinador =
            from t in _context.Treinadores.AsNoTracking()
            join s in _context.AssinaturasTreinador.AsNoTracking() on t.Id equals s.TreinadorId
            select new { t.ContaId, s.Status, s.DataCancelamento };

        var elegiveisAluno = contasAluno
            .GroupBy(x => x.ContaId)
            .Where(g => g.All(x => x.Status == AssinaturaAlunoStatus.Cancelada
                                   && x.DataCancelamento != null && x.DataCancelamento < threshold))
            .Select(g => g.Key);

        var elegiveisTreinador = contasTreinador
            .GroupBy(x => x.ContaId)
            .Where(g => g.All(x => x.Status == AssinaturaTreinadorStatus.Cancelada
                                   && x.DataCancelamento != null && x.DataCancelamento < threshold))
            .Select(g => g.Key);

        var candidatos = await elegiveisAluno
            .Union(elegiveisTreinador)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (candidatos.Count == 0)
            return Array.Empty<Guid>();

        return await _context.Contas.AsNoTracking()
            .Where(c => candidatos.Contains(c.Id) && c.AnonimizadaEm == null)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
