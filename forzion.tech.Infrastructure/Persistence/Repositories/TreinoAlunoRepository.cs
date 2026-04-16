using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Infrastructure.Persistence.Repositories;

public class TreinoAlunoRepository(AppDbContext context) : ITreinoAlunoRepository
{
    private readonly AppDbContext _context = context;

    public async Task<TreinoAluno?> ObterAsync(Guid treinoId, Guid alunoId, CancellationToken cancellationToken = default) =>
        await _context.TreinoAlunos
            .FirstOrDefaultAsync(ta => ta.TreinoId == treinoId && ta.AlunoId == alunoId, cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarAsync(TreinoAluno treinoAluno, CancellationToken cancellationToken = default) =>
        await _context.TreinoAlunos.AddAsync(treinoAluno, cancellationToken).ConfigureAwait(false);
}
