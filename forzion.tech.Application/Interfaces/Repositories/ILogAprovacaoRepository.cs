using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.Interfaces.Repositories;

public interface ILogAprovacaoRepository
{
    Task AdicionarAsync(LogAprovacao log, CancellationToken cancellationToken = default);
}
