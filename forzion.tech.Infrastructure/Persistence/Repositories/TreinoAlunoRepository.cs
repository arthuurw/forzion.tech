using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class TreinoAlunoRepository(AppDbContext context) : ITreinoAlunoRepository
{
    private readonly AppDbContext _context = context;

    public async Task<TreinoAluno?> ObterAsync(Guid treinoId, Guid alunoId, CancellationToken cancellationToken = default) =>
        await _context.TreinoAlunos
            .FirstOrDefaultAsync(ta => ta.TreinoId == treinoId && ta.AlunoId == alunoId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<int> ContarAtivosPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default) =>
        await _context.TreinoAlunos
            .CountAsync(ta => ta.AlunoId == alunoId && ta.Status == TreinoAlunoStatus.Ativo, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<TreinoAluno>> ListarAtivosPorParAsync(Guid treinadorId, Guid alunoId, CancellationToken cancellationToken = default) =>
        await _context.TreinoAlunos
            .Where(ta => ta.AlunoId == alunoId && ta.Status == TreinoAlunoStatus.Ativo &&
                         _context.Treinos.Any(t => t.Id == ta.TreinoId && t.TreinadorId == treinadorId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<TreinoAluno>> ListarAtivosPorAlunoAsync(Guid alunoId, CancellationToken cancellationToken = default) =>
        await _context.TreinoAlunos
            .Where(ta => ta.AlunoId == alunoId && ta.Status == TreinoAlunoStatus.Ativo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarAsync(TreinoAluno treinoAluno, CancellationToken cancellationToken = default) =>
        await _context.TreinoAlunos.AddAsync(treinoAluno, cancellationToken).ConfigureAwait(false);
}
