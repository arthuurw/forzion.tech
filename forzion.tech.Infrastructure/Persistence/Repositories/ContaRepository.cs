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

    public async Task<DateTimeOffset?> ObterEpochSessaoAsync(Guid contaId, CancellationToken cancellationToken = default) =>
        await _context.Contas.AsNoTracking()
            .Where(c => c.Id == contaId)
            .Select(c => c.SessoesInvalidasAntesDeUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarAsync(Conta conta, CancellationToken cancellationToken = default) =>
        await _context.Contas.AddAsync(conta, cancellationToken).ConfigureAwait(false);

    public async Task<int> ContarCriadasDesdeAsync(DateTime desde, CancellationToken cancellationToken = default) =>
        await _context.Contas
            .CountAsync(c => c.CreatedAt >= desde, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Guid>> ListarElegivelPurgaLgpdAsync(DateTime threshold, CancellationToken cancellationToken = default)
    {
        // Conta elegível: não anonimizada, teve ao menos uma assinatura (aluno OU treinador) e
        // TODAS estão Canceladas com DataCancelamento anterior ao threshold (retenção fiscal 5 anos).
        // Uma query via EXISTS/NOT EXISTS (Any) — sem 2º round-trip nem IN inflado.
        var alunoIds = _context.Alunos.AsNoTracking();
        var treinadorIds = _context.Treinadores.AsNoTracking();

        return await _context.Contas.AsNoTracking()
            .Where(c => c.AnonimizadaEm == null)
            .Where(c =>
                alunoIds.Any(a => a.ContaId == c.Id
                    && _context.AssinaturaAlunos.Any(s => s.AlunoId == a.Id))
                || treinadorIds.Any(t => t.ContaId == c.Id
                    && _context.AssinaturasTreinador.Any(s => s.TreinadorId == t.Id)))
            .Where(c =>
                !alunoIds.Any(a => a.ContaId == c.Id
                    && _context.AssinaturaAlunos.Any(s => s.AlunoId == a.Id
                        && (s.Status != AssinaturaAlunoStatus.Cancelada
                            || s.DataCancelamento == null || s.DataCancelamento >= threshold)))
                && !treinadorIds.Any(t => t.ContaId == c.Id
                    && _context.AssinaturasTreinador.Any(s => s.TreinadorId == t.Id
                        && (s.Status != AssinaturaTreinadorStatus.Cancelada
                            || s.DataCancelamento == null || s.DataCancelamento >= threshold))))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ContaTesteResumo>> ListarTesteAsync(string dominio, CancellationToken cancellationToken = default)
    {
        var contas = await _context.Contas.AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return contas
            .Where(c => c.Email.Value.EndsWith(dominio, StringComparison.OrdinalIgnoreCase))
            .Select(c => new ContaTesteResumo(c.Id, c.Email.Value, c.CreatedAt))
            .ToList();
    }

    public async Task ExcluirAsync(Conta conta, CancellationToken cancellationToken = default) =>
        await _context.Contas
            .Where(c => c.Id == conta.Id)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
}
